"""Spôsoby hľadania, ktoré porovnávame. Každý dostane dopyt a vráti zoradené (kľúč, skóre).

- KeywordRetriever  – to, čo dnes robí C# API (baseline)
- Bm25Retriever     – klasické fulltextové hľadanie, silnejší „súper“
- SemanticRetriever – embedding model (krok 2)
- HybridRetriever   – spojenie BM25 a sémantiky (Reciprocal Rank Fusion), bez filtra
- ServedHybridRetriever – presne to, čo vracia endpoint /search: hybrid + minimálne skóre

BM25, RRF aj filter sú importované z produkčného kódu (text_search, hybrid) – meriame to, čo beží.
"""

from collections.abc import Sequence
from typing import Protocol

import numpy as np

from ai_service.embeddings import Embedder
from ai_service.evaluation.dataset import EvalListing
from ai_service.hybrid import fuse
from ai_service.text_search import Bm25Index, normalize, reciprocal_rank_fusion

type Ranking = list[tuple[str, float]]  # [(kľúč inzerátu, skóre)], najlepší prvý


class Retriever(Protocol):
    name: str

    def rank(self, query: str) -> Ranking: ...


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


class Bm25Retriever:
    name = "BM25"

    def __init__(self, listings: Sequence[EvalListing]) -> None:
        # Rovnaký text ako v službe (ListingDocument.search_text)
        self.index = Bm25Index([(listing.key, listing.document.search_text()) for listing in listings])

    def rank(self, query: str) -> Ranking:
        return [(hit.key, hit.score) for hit in self.index.rank(query)]


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


class HybridRetriever:
    """RRF spojenie ľubovoľných metód, bez filtra – ukazuje strop, ktorý hybrid dosiahne."""

    def __init__(self, *retrievers: Retriever) -> None:
        self._retrievers = retrievers
        self.name = "hybrid: " + " + ".join(r.name for r in retrievers)

    def rank(self, query: str) -> Ranking:
        return reciprocal_rank_fusion([[key for key, _ in r.rank(query)] for r in self._retrievers])


class ServedHybridRetriever:
    """To isté, čo endpoint /search: BM25 + sémantika cez RRF a filter minimálneho skóre (hybrid.fuse)."""

    def __init__(self, bm25: Bm25Retriever, semantic: SemanticRetriever, min_score: float) -> None:
        self._bm25, self._semantic, self._min_score = bm25, semantic, min_score
        self.name = f"hybrid as served (min. score {min_score})"

    def rank(self, query: str) -> Ranking:
        results = fuse(self._semantic.rank(query), self._bm25.index.rank(query), self._min_score)
        return [(r.key, r.semantic_score or 0.0) for r in results]
