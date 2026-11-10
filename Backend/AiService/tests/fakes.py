"""Falošné služby pre testy API: rýchle, bez modelu, databázy a siete.

Testujeme tak NAŠU logiku (indexovanie, poradie výsledkov, chyby), nie kvalitu modelu –
tú meria samostatný test s markerom `model` a neskôr vyhodnotenie (krok 3).
"""

from collections.abc import Sequence
from uuid import UUID

import numpy as np
from numpy.typing import NDArray

from ai_service.listing_text import ListingDocument
from ai_service.repository import Hit, KeywordDocument, StoredEmbedding

# Každá „téma“ má vlastný rozmer vektora: text o logu má veľké číslo na pozícii 0, o sieti na pozícii 1…
TOPICS = [
    ("logo", "grafik", "znak", "etiket"),
    ("wifi", "sieť", "siet", "počítač", "tlačiar"),
    ("e-shop", "eshop", "obchod", "predáv"),
]


class FakeEmbedder:
    model_name = "fake-model"
    dimension = len(TOPICS) + 1

    def __init__(self) -> None:
        self.embedded_texts: list[str] = []  # aby test videl, čo sa počítalo

    def embed(self, texts: list[str]) -> NDArray[np.float32]:
        self.embedded_texts.extend(texts)
        rows = []
        for text in texts:
            lower = text.lower()
            vector = [float(sum(lower.count(word) for word in topic)) for topic in TOPICS]
            vector.append(0.1)  # aby nebol nikdy nulový vektor
            rows.append(vector)
        matrix = np.array(rows, dtype=np.float32)
        return matrix / np.linalg.norm(matrix, axis=1, keepdims=True)


class InMemoryRepository:
    def __init__(self) -> None:
        self.rows: dict[UUID, StoredEmbedding] = {}

    def ensure_schema(self) -> None:
        pass

    def content_hashes(self) -> dict[UUID, str]:
        return {listing_id: row.content_hash for listing_id, row in self.rows.items()}

    def keyword_documents(self) -> list[KeywordDocument]:
        return [KeywordDocument(r.listing_id, r.title, r.status, r.search_text) for r in self.rows.values()]

    def upsert(self, rows: Sequence[StoredEmbedding], model_name: str) -> None:
        for row in rows:
            self.rows[row.listing_id] = row

    def delete_except(self, keep: set[UUID]) -> int:
        removed = [listing_id for listing_id in self.rows if listing_id not in keep]
        for listing_id in removed:
            del self.rows[listing_id]
        return len(removed)

    def _rank(self, query: NDArray[np.float32], exclude: UUID | None, limit: int) -> list[Hit]:
        hits = [
            Hit(row.listing_id, row.title, row.status, float(np.dot(row.embedding, query)))
            for row in self.rows.values()
            if row.listing_id != exclude
        ]
        return sorted(hits, key=lambda h: h.score, reverse=True)[:limit]

    def search(self, query: NDArray[np.float32], limit: int) -> list[Hit]:
        return self._rank(query, None, limit)

    def similar(self, listing_id: UUID, limit: int) -> list[Hit] | None:
        row = self.rows.get(listing_id)
        return None if row is None else self._rank(row.embedding, listing_id, limit)


class InMemoryListings:
    def __init__(self, documents: list[ListingDocument]) -> None:
        self.documents = documents

    def fetch_all(self) -> list[ListingDocument]:
        return list(self.documents)


def document(listing_id: int, title: str, description: str, status: str = "open") -> ListingDocument:
    return ListingDocument(
        id=UUID(int=listing_id),
        title=title,
        category="test",
        description=description,
        skills=(),
        status=status,
    )
