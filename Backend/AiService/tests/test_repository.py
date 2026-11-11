"""Úložisko vektorov proti skutočnému PostgreSQL s pgvector v Dockeri (Testcontainers).

Obdoba C# integračných testov: overí skutočné SQL – operátor <=>, HNSW index, upsert a mazanie.
Vyžaduje bežiaci Docker.
"""

from collections.abc import Iterator
from uuid import UUID

import numpy as np
import pytest
from testcontainers.community.postgres import PostgresContainer

from ai_service.repository import PostgresEmbeddingRepository, StoredEmbedding

DIMENSION = 3


def vector(*values: float) -> np.ndarray:
    v = np.array(values, dtype=np.float32)
    return v / np.linalg.norm(v)


def row(listing_id: int, title: str, embedding: np.ndarray, content_hash: str = "h") -> StoredEmbedding:
    return StoredEmbedding(UUID(int=listing_id), title, "open", content_hash, embedding, f"{title} text")


@pytest.fixture(scope="module")
def database_url() -> Iterator[str]:
    # Oficiálny obraz PostgreSQL 17 s rozšírením pgvector – jeden kontajner pre všetky testy v súbore
    with PostgresContainer("pgvector/pgvector:pg17", driver=None) as postgres:
        yield postgres.get_connection_url()


@pytest.fixture
def repository(database_url: str) -> PostgresEmbeddingRepository:
    repo = PostgresEmbeddingRepository(database_url, DIMENSION)
    repo.ensure_schema()
    repo.delete_except(set())  # každý test začína s prázdnou tabuľkou
    return repo


def test_ensure_schema_can_run_repeatedly(repository: PostgresEmbeddingRepository) -> None:
    repository.ensure_schema()  # druhé spustenie nesmie spadnúť (IF NOT EXISTS)


def test_search_orders_by_cosine_similarity(repository: PostgresEmbeddingRepository) -> None:
    repository.upsert(
        [
            row(1, "x-ová os", vector(1, 0, 0)),
            row(2, "y-ová os", vector(0, 1, 0)),
            row(3, "medzi", vector(1, 1, 0)),
        ],
        "m",
    )

    hits = repository.search(vector(1, 0.1, 0), limit=3)

    assert [h.title for h in hits] == ["x-ová os", "medzi", "y-ová os"]
    assert hits[0].score == pytest.approx(0.995, abs=0.01)


def test_upsert_overwrites_existing_row(repository: PostgresEmbeddingRepository) -> None:
    repository.upsert([row(1, "starý", vector(1, 0, 0), content_hash="a")], "m")
    repository.upsert([row(1, "nový", vector(0, 1, 0), content_hash="b")], "m")

    assert repository.content_hashes() == {UUID(int=1): "b"}
    assert repository.search(vector(0, 1, 0), limit=1)[0].title == "nový"


def test_similar_excludes_itself_and_unknown_returns_none(repository: PostgresEmbeddingRepository) -> None:
    repository.upsert([row(1, "a", vector(1, 0, 0)), row(2, "b", vector(0.9, 0.1, 0))], "m")

    hits = repository.similar(UUID(int=1), limit=5)

    assert hits is not None
    assert [h.title for h in hits] == ["b"]
    assert repository.similar(UUID(int=999), limit=5) is None


def test_delete_except_removes_others(repository: PostgresEmbeddingRepository) -> None:
    repository.upsert([row(1, "a", vector(1, 0, 0)), row(2, "b", vector(0, 1, 0))], "m")

    removed = repository.delete_except({UUID(int=1)})

    assert removed == 1
    assert set(repository.content_hashes()) == {UUID(int=1)}
