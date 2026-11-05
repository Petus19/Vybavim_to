"""Štyri spôsoby hľadania, ktoré porovnávame. Každý dostane dopyt a vráti zoradené (kľúč, skóre).

- KeywordRetriever  – to, čo dnes robí C# API (baseline)
- Bm25Retriever     – klasické fulltextové hľadanie, silnejší „súper“
- SemanticRetriever – embedding model (krok 2)
- HybridRetriever   – spojenie BM25 a sémantiky (Reciprocal Rank Fusion)
"""

import math
import re
import unicodedata
from collections import Counter
from collections.abc import Sequence
from typing import Protocol

import numpy as np

from ai_service.embeddings import Embedder
from ai_service.evaluation.dataset import EvalListing

type Ranking = list[tuple[str, float]]  # [(kľúč inzerátu, skóre)], najlepší prvý


class Retriever(Protocol):
    name: str

    def rank(self, query: str) -> Ranking: ...


def normalize(text: str) -> str:
    """Bez diakritiky a malými písmenami – obdoba unaccent() + ILIKE v PostgreSQL.

    NFKD rozloží „č“ na „c“ + samostatný háčik; háčiky a dĺžne (kombinujúce znaky) potom zahodíme.
    """
    decomposed = unicodedata.normalize("NFKD", text)
    return "".join(ch for ch in decomposed if not unicodedata.combining(ch)).casefold()


def keyword_text(listing: EvalListing) -> str:
    """Presne tie polia, v ktorých hľadá C# API: názov + popis + zručnosti (kategóriu nie)."""
    doc = listing.document
    return f"{doc.title} {doc.description} {' '.join(doc.skills)}"


class KeywordRetriever:
    """Verná kópia C# SearchListings: celý dopyt ako JEDEN podreťazec (`ILIKE '%dopyt%'`).

    Neradí podľa relevancie – zhody vracia v poradí inzerátov (v C# „najnovšie“), všetky so skóre 1.
    """

    name = "keyword (current C# API)"

    def __init__(self, listings: Sequence[EvalListing]) -> None:
        self._texts = [(listing.key, normalize(keyword_text(listing))) for listing in listings]

    def rank(self, query: str) -> Ranking:
        needle = normalize(query.strip())
        return [(key, 1.0) for key, text in self._texts if needle in text]


def tokenize(text: str, prefix_length: int) -> list[str]:
    """Slová bez diakritiky, skrátené na prvých `prefix_length` znakov.

    Skracovanie je najjednoduchší „stemming“ pre slovenčinu: „kaviareň“, „kaviarne“, „kaviarni“
    → všetko „kavia“. Bez neho by BM25 nespojil rôzne pády toho istého slova.
    """
    return [word[:prefix_length] for word in re.findall(r"\w+", normalize(text))]


class Bm25Retriever:
    """BM25 – predvolený spôsob radenia v Lucene, Elasticsearch a OpenSearch.

    Skóre slova je vyššie, keď je v inzeráte častejšie (tf), ale so stropom (k1), keď je vzácne
    v celej sade (idf) a keď je inzerát kratší (b). Skóre dopytu = súčet cez jeho slová.
    """

    name = "BM25"

    def __init__(
        self, listings: Sequence[EvalListing], k1: float = 1.5, b: float = 0.75, prefix_length: int = 5
    ) -> None:
        self._k1, self._b, self._prefix_length = k1, b, prefix_length
        self._keys = [listing.key for listing in listings]
        self._term_counts = [
            Counter(tokenize(f"{keyword_text(listing)} {listing.document.category}", prefix_length))
            for listing in listings
        ]
        self._lengths = [sum(counts.values()) for counts in self._term_counts]
        self._average_length = sum(self._lengths) / len(self._lengths)
        document_frequency = Counter(term for counts in self._term_counts for term in counts)
        n = len(listings)
        self._idf = {
            term: math.log((n - df + 0.5) / (df + 0.5) + 1) for term, df in document_frequency.items()
        }

    def rank(self, query: str) -> Ranking:
        terms = tokenize(query, self._prefix_length)
        scored = []
        for key, counts, length in zip(self._keys, self._term_counts, self._lengths, strict=True):
            score = 0.0
            for term in terms:
                tf = counts.get(term, 0)
                if tf:
                    norm = self._k1 * (1 - self._b + self._b * length / self._average_length)
                    score += self._idf[term] * tf * (self._k1 + 1) / (tf + norm)
            if score > 0:
                scored.append((key, score))
        return sorted(scored, key=lambda item: -item[1])  # sorted je stabilný – zhody ostanú v poradí sady


class SemanticRetriever:
    """Kosínusová podobnosť embeddingov – rovnaká matematika ako `<=>` v pgvector, len v pamäti.

    Pri 60 inzerátoch netreba databázu ani HNSW: porovnáme dopyt so všetkými (presné hľadanie).
    Niektoré modely (rodina E5) chcú pred textom predponu „query: “ / „passage: “.
    """

    def __init__(
        self,
        embedder: Embedder,
        listings: Sequence[EvalListing],
        query_prefix: str = "",
        passage_prefix: str = "",
    ) -> None:
        self.name = f"semantic ({embedder.model_name.rsplit('/', 1)[-1]})"
        self._embedder, self._query_prefix = embedder, query_prefix
        self._keys = [listing.key for listing in listings]
        self._matrix = embedder.embed([passage_prefix + listing.document.text() for listing in listings])

    def rank(self, query: str) -> Ranking:
        query_vector = self._embedder.embed([self._query_prefix + query])[0]
        scores = self._matrix @ query_vector  # normalizované vektory → skalárny súčin = kosínus
        order = np.argsort(-scores, kind="stable")
        return [(self._keys[i], float(scores[i])) for i in order]


def reciprocal_rank_fusion(rankings: Sequence[Ranking], k: int = 60) -> Ranking:
    """Spojí viac poradí do jedného: každý zoznam dá inzerátu 1 / (k + poradie), body sa sčítajú.

    Používa iba POZÍCIE, nie skóre – preto sa dajú spojiť aj metódy s úplne inými škálami
    (BM25 dáva 0–15, kosínus 0–1). k = 60 je hodnota z pôvodného článku (Cormack a kol., 2009).
    """
    fused: dict[str, float] = {}
    for ranking in rankings:
        for position, (key, _) in enumerate(ranking, start=1):
            fused[key] = fused.get(key, 0.0) + 1 / (k + position)
    return sorted(fused.items(), key=lambda item: -item[1])


class HybridRetriever:
    def __init__(self, *retrievers: Retriever) -> None:
        self._retrievers = retrievers
        self.name = "hybrid: " + " + ".join(r.name for r in retrievers)

    def rank(self, query: str) -> Ranking:
        return reciprocal_rank_fusion([r.rank(query) for r in self._retrievers])
