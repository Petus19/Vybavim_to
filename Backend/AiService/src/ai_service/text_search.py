"""Hľadanie podľa slov (BM25) a spájanie poradí (Reciprocal Rank Fusion).

Používa ho hybridné hľadanie služby aj vyhodnotenie (evaluation/) – tá istá implementácia,
takže čísla z vyhodnotenia platia pre to, čo služba naozaj robí.
"""

import math
import re
import unicodedata
from collections import Counter
from collections.abc import Hashable, Sequence
from dataclasses import dataclass

PREFIX_LENGTH = 5

# Slová, ktoré nič nehovoria o tom, ČO človek hľadá (predložky, spojky, „chcem“, „potrebujem“…).
# Bez nich by „oprava práčky“ našla každý inzerát s predložkou „na“. Sú bez diakritiky – porovnávajú
# sa po normalize().
# fmt: off
STOPWORDS = frozenset([
    "a", "aj", "ako", "ale", "alebo", "ani", "bez", "by", "cez", "co", "do", "je", "k", "ku", "ma",
    "mam", "mame", "mi", "moj", "moja", "moje", "na", "nad", "nam", "nas", "nasa", "nase", "nasej",
    "nasu", "nieco", "niekoho", "niekto", "no", "o", "od", "po", "pod", "pre", "pri", "s", "sa", "si",
    "sme", "so", "som", "ste", "su", "ta", "tak", "to", "ten", "tie", "u", "uz", "v", "vo", "z", "za",
    "ze", "zo", "chcem", "chceme", "potrebujem", "potrebujeme", "hladam", "hladame", "kto", "ktory",
    "ktora", "ktore", "ktori", "nejaky", "nejaka", "nejake", "nejaku", "aby", "len", "iba", "mne",
    "mna", "eur", "euro", "eura", "rozpocet", "rozpoctom",
])
# fmt: on


def normalize(text: str) -> str:
    """Bez diakritiky a malými písmenami – obdoba unaccent() + ILIKE v PostgreSQL.

    NFKD rozloží „č“ na „c“ + samostatný háčik; háčiky a dĺžne (kombinujúce znaky) potom zahodíme.
    """
    decomposed = unicodedata.normalize("NFKD", text)
    return "".join(ch for ch in decomposed if not unicodedata.combining(ch)).casefold()


def terms(text: str, prefix_length: int = PREFIX_LENGTH) -> list[str]:
    """Slová bez diakritiky a bez výplňových slov, skrátené na prvých `prefix_length` znakov.

    Skracovanie je najjednoduchší „stemming“ pre slovenčinu: „kaviareň“, „kaviarne“, „kaviarni“
    → všetko „kavia“. Bez neho by BM25 nespojil rôzne pády toho istého slova.
    Spojovník medzi písmenami sa zahodí: „Wi-Fi“ = „wifi“, „e-shop“ = „eshop“.
    Čísla sa vynechajú – „do 300 €“ je rozpočet, nie téma inzerátu.
    """
    words = re.findall(r"\w+", re.sub(r"(?<=\w)-(?=\w)", "", normalize(text)))
    return [
        word[:prefix_length]
        for word in words
        if len(word) > 1 and word not in STOPWORDS and not word.isdigit()
    ]


@dataclass(frozen=True)
class KeywordHit[K: Hashable]:
    key: K
    score: float
    full_match: bool  # inzerát obsahuje VŠETKY hľadané slová – silný signál, že je relevantný


class Bm25Index[K: Hashable]:
    """BM25 – predvolený spôsob radenia v Lucene, Elasticsearch a OpenSearch.

    Skóre slova je vyššie, keď je v inzeráte častejšie (tf), ale so stropom (k1), keď je vzácne
    v celej sade (idf) a keď je inzerát kratší (b). Skóre dopytu = súčet cez jeho slová.
    """

    def __init__(
        self,
        documents: Sequence[tuple[K, str]],
        k1: float = 1.5,
        b: float = 0.75,
        prefix_length: int = PREFIX_LENGTH,
    ) -> None:
        self._k1, self._b, self._prefix_length = k1, b, prefix_length
        self._keys = [key for key, _ in documents]
        self._term_counts = [Counter(terms(text, prefix_length)) for _, text in documents]
        self._lengths = [sum(counts.values()) for counts in self._term_counts]
        self._average_length = (sum(self._lengths) / len(self._lengths)) if documents else 0.0
        document_frequency = Counter(term for counts in self._term_counts for term in counts)
        n = len(documents)
        self._idf = {
            term: math.log((n - df + 0.5) / (df + 0.5) + 1) for term, df in document_frequency.items()
        }

    def __len__(self) -> int:
        return len(self._keys)

    def rank(self, query: str) -> list[KeywordHit[K]]:
        """Inzeráty s aspoň jedným hľadaným slovom, najlepší prvý."""
        query_terms = set(terms(query, self._prefix_length))
        hits = []
        for key, counts, length in zip(self._keys, self._term_counts, self._lengths, strict=True):
            score = 0.0
            for term in query_terms:
                tf = counts.get(term, 0)
                if tf:
                    norm = self._k1 * (1 - self._b + self._b * length / self._average_length)
                    score += self._idf[term] * tf * (self._k1 + 1) / (tf + norm)
            if score > 0:
                hits.append(KeywordHit(key, score, full_match=all(t in counts for t in query_terms)))
        return sorted(hits, key=lambda hit: -hit.score)  # sorted je stabilný – zhody ostanú v poradí


def reciprocal_rank_fusion[K: Hashable](
    rankings: Sequence[Sequence[K]], k: int = 60
) -> list[tuple[K, float]]:
    """Spojí viac poradí do jedného: každý zoznam dá položke 1 / (k + poradie), body sa sčítajú.

    Používa iba POZÍCIE, nie skóre – preto sa dajú spojiť aj metódy s úplne inými škálami
    (BM25 dáva 0–15, kosínus 0–1). k = 60 je hodnota z pôvodného článku (Cormack a kol., 2009).
    """
    fused: dict[K, float] = {}
    for ranking in rankings:
        for position, key in enumerate(ranking, start=1):
            fused[key] = fused.get(key, 0.0) + 1 / (k + position)
    return sorted(fused.items(), key=lambda item: -item[1])
