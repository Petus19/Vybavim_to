"""Hybridné hľadanie: BM25 (slová) + embeddingy (význam), spojené cez RRF, s minimálnym skóre.

Prečo hybrid: vyhodnotenie (README, krok 3) ukázalo recall@5 0.91 oproti 0.86 pre samotnú sémantiku.
Prečo minimálne skóre: sémantické hľadanie inak VŽDY niečo vráti, aj na „niečo s účtovníctvom“.
"""

from collections.abc import Hashable, Sequence
from dataclasses import dataclass
from uuid import UUID

from ai_service.embeddings import Embedder
from ai_service.repository import EmbeddingRepository, KeywordDocument
from ai_service.text_search import Bm25Index, KeywordHit, reciprocal_rank_fusion

# Kosínusová podobnosť, od ktorej výsledok považujeme za relevantný už len podľa významu (model MiniLM).
# Z vyhodnotenia (hybrid as served): pri 0.45 recall@5 0.89 a 5 zo 6 dopytov bez odpovede vráti prázdny
# zoznam. Cesta cez slová zachráni presné zhody, takže sémantika môže mať prísnejšiu hranicu ako sama (0.41).
DEFAULT_MIN_SCORE = 0.45

# Nižšie skóre stačí, keď sa zhoduje aspoň časť hľadaných slov – oba signály sa zhodnú.
# Na demo dátach: relevantné čiastočné zhody ~0.27–0.31 („wifi“, „penzión“), nerelevantné ~0.07.
PARTIAL_MIN_SCORE = 0.25

# Koľko najbližších vektorov si vypýtame z pgvector, kým ich spojíme s BM25
SEMANTIC_CANDIDATES = 50


@dataclass(frozen=True)
class Fused[K: Hashable]:
    key: K
    semantic_score: float | None  # None = inzerát nebol medzi sémantickými kandidátmi
    keyword_match: bool  # obsahuje všetky hľadané slová


def fuse[K: Hashable](
    semantic: Sequence[tuple[K, float]],
    keyword: Sequence[KeywordHit[K]],
    min_score: float,
    partial_min_score: float = PARTIAL_MIN_SCORE,
) -> list[Fused[K]]:
    """Spojí obe poradia a nechá iba relevantné výsledky. Výsledok ostane, ak:

    (a) je významovo dosť blízko (kosínus ≥ min_score), alebo
    (b) obsahuje všetky hľadané slová – napr. „WooCommerce“ či „SEO“, kde model nemusí byť sebaistý, alebo
    (c) obsahuje aspoň časť slov a význam je aspoň slabo blízky (≥ partial_min_score).

    Samotná čiastočná zhoda slov nestačí: „oprava práčky“ nesmie nájsť „Oprava chýb na webe“.
    Poradie: najprv silné dôkazy (a, b), potom slabšie (c); v rámci skupiny poradie podľa RRF.
    """
    semantic_scores = dict(semantic)
    full_matches = {hit.key for hit in keyword if hit.full_match}
    partial_matches = {hit.key for hit in keyword}
    fused = reciprocal_rank_fusion([[key for key, _ in semantic], [hit.key for hit in keyword]])

    strong, weak = [], []
    for key, _ in fused:
        score = semantic_scores.get(key)
        strong_meaning = score is not None and score >= min_score
        weak_meaning = score is not None and score >= min(partial_min_score, min_score)
        result = Fused(key, score, key in full_matches)
        if strong_meaning or key in full_matches:
            strong.append(result)
        elif key in partial_matches and weak_meaning:
            weak.append(result)
    return strong + weak


@dataclass(frozen=True)
class HybridHit:
    listing_id: UUID
    title: str
    status: str
    semantic_score: float | None
    keyword_match: bool


class HybridSearch:
    """Drží BM25 index nad textami z vlastnej tabuľky a pri hľadaní ho spojí s pgvector."""

    def __init__(self, embedder: Embedder, repository: EmbeddingRepository) -> None:
        self._embedder = embedder
        self._repository = repository
        self._documents: dict[UUID, KeywordDocument] = {}
        self._index: Bm25Index[UUID] = Bm25Index([])

    def refresh(self) -> None:
        """Prestaví BM25 index – po štarte a po každom /index. Pri stovkách inzerátov trvá milisekundy."""
        documents = self._repository.keyword_documents()
        index = Bm25Index([(d.listing_id, d.search_text) for d in documents])
        # Dve priradenia za sebou bez await – súbežná požiadavka nevidí polovičný stav
        self._documents, self._index = {d.listing_id: d for d in documents}, index

    def search(self, query: str, limit: int, min_score: float = DEFAULT_MIN_SCORE) -> list[HybridHit]:
        documents, index = self._documents, self._index
        query_vector = self._embedder.embed([query])[0]
        semantic = [
            (h.listing_id, h.score) for h in self._repository.search(query_vector, SEMANTIC_CANDIDATES)
        ]

        hits = []
        for result in fuse(semantic, index.rank(query), min_score)[:limit]:
            document = documents.get(result.key)
            if document is None:  # zaindexované medzičasom, BM25 index ešte nie je obnovený
                continue
            hits.append(
                HybridHit(
                    result.key, document.title, document.status, result.semantic_score, result.keyword_match
                )
            )
        return hits
