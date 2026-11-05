"""Uloženie vektorov v PostgreSQL (rozšírenie pgvector) a hľadanie najbližších susedov.

Služba má vlastnú schému "ai" – rovnaké pravidlo ako moduly C# API: do cudzích tabuliek nesiaha.
"""

from collections.abc import Iterator, Sequence
from contextlib import contextmanager
from dataclasses import dataclass
from typing import Protocol
from uuid import UUID

import numpy as np
import psycopg
from numpy.typing import NDArray
from pgvector.psycopg import register_vector
from psycopg import sql


@dataclass(frozen=True)
class StoredEmbedding:
    """Jeden riadok na uloženie: inzerát, jeho vektor a odtlačok textu, z ktorého vznikol."""

    listing_id: UUID
    title: str
    status: str
    content_hash: str
    embedding: NDArray[np.float32]


@dataclass(frozen=True)
class Hit:
    """Výsledok hľadania. score = kosínusová podobnosť: 1 = rovnaký význam, okolo 0 = nesúvisí."""

    listing_id: UUID
    title: str
    status: str
    score: float


class EmbeddingRepository(Protocol):
    """Rozhranie úložiska – v testoch API ho nahradí verzia v pamäti."""

    def ensure_schema(self) -> None: ...
    def content_hashes(self) -> dict[UUID, str]: ...
    def upsert(self, rows: Sequence[StoredEmbedding], model_name: str) -> None: ...
    def delete_except(self, keep: set[UUID]) -> int: ...
    def search(self, query: NDArray[np.float32], limit: int) -> list[Hit]: ...
    def similar(self, listing_id: UUID, limit: int) -> list[Hit] | None: ...


class PostgresEmbeddingRepository:
    def __init__(self, database_url: str, dimension: int) -> None:
        self._database_url = database_url
        self._dimension = dimension

    @contextmanager
    def _connect(self) -> Iterator[psycopg.Connection]:
        # Jedno spojenie na operáciu – pri pár požiadavkách za minútu stačí; pri záťaži connection pool.
        with psycopg.connect(self._database_url) as conn:
            register_vector(conn)  # naučí psycopg posielať a čítať typ vector ako numpy pole
            yield conn

    def ensure_schema(self) -> None:
        """Vytvorí rozšírenie, schému, tabuľku a index, ak ešte neexistujú (bezpečné spustiť opakovane)."""
        with psycopg.connect(self._database_url, autocommit=True) as conn:
            conn.execute("CREATE EXTENSION IF NOT EXISTS vector")
            conn.execute("CREATE SCHEMA IF NOT EXISTS ai")
            conn.execute(
                sql.SQL(
                    """
                    CREATE TABLE IF NOT EXISTS ai.listing_embeddings (
                        listing_id   uuid PRIMARY KEY,
                        title        text NOT NULL,
                        status       text NOT NULL,
                        model        text NOT NULL,
                        content_hash text NOT NULL,
                        embedding    vector({dimension}) NOT NULL,
                        indexed_at   timestamptz NOT NULL DEFAULT now()
                    )
                    """
                ).format(dimension=sql.Literal(self._dimension))
            )
            # HNSW = graf „susedov“, vďaka ktorému sa najbližšie vektory nájdu bez porovnania so všetkými.
            # vector_cosine_ops: index počíta s kosínusovou vzdialenosťou (operátor <=>).
            conn.execute(
                "CREATE INDEX IF NOT EXISTS ix_listing_embeddings_hnsw "
                "ON ai.listing_embeddings USING hnsw (embedding vector_cosine_ops)"
            )

    def content_hashes(self) -> dict[UUID, str]:
        with self._connect() as conn:
            rows = conn.execute("SELECT listing_id, content_hash FROM ai.listing_embeddings").fetchall()
        return {listing_id: content_hash for listing_id, content_hash in rows}

    def upsert(self, rows: Sequence[StoredEmbedding], model_name: str) -> None:
        """Vloží nové riadky, existujúce prepíše (INSERT … ON CONFLICT DO UPDATE)."""
        if not rows:
            return
        with self._connect() as conn, conn.cursor() as cur:
            cur.executemany(
                """
                INSERT INTO ai.listing_embeddings (listing_id, title, status, model, content_hash, embedding)
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT (listing_id) DO UPDATE SET
                    title = EXCLUDED.title, status = EXCLUDED.status, model = EXCLUDED.model,
                    content_hash = EXCLUDED.content_hash, embedding = EXCLUDED.embedding, indexed_at = now()
                """,
                [(r.listing_id, r.title, r.status, model_name, r.content_hash, r.embedding) for r in rows],
            )

    def delete_except(self, keep: set[UUID]) -> int:
        """Zmaže vektory inzerátov, ktoré už nie sú verejné (napr. stiahnuté). Vráti počet zmazaných."""
        with self._connect() as conn:
            cur = conn.execute(
                "DELETE FROM ai.listing_embeddings WHERE NOT (listing_id = ANY(%s))", [list(keep)]
            )
            return cur.rowcount

    def search(self, query: NDArray[np.float32], limit: int) -> list[Hit]:
        # <=> je kosínusová VZDIALENOSŤ (0 = rovnaký smer); podobnosť = 1 - vzdialenosť
        with self._connect() as conn:
            rows = conn.execute(
                """
                SELECT listing_id, title, status, 1 - (embedding <=> %(q)s) AS score
                FROM ai.listing_embeddings
                ORDER BY embedding <=> %(q)s
                LIMIT %(limit)s
                """,
                {"q": query, "limit": limit},
            ).fetchall()
        return [Hit(*row) for row in rows]

    def similar(self, listing_id: UUID, limit: int) -> list[Hit] | None:
        """Inzeráty najbližšie danému inzerátu (bez neho samého). None, ak inzerát nie je zaindexovaný."""
        with self._connect() as conn:
            row = conn.execute(
                "SELECT embedding FROM ai.listing_embeddings WHERE listing_id = %s", [listing_id]
            ).fetchone()
            if row is None:
                return None
            rows = conn.execute(
                """
                SELECT listing_id, title, status, 1 - (embedding <=> %(q)s) AS score
                FROM ai.listing_embeddings
                WHERE listing_id <> %(id)s
                ORDER BY embedding <=> %(q)s
                LIMIT %(limit)s
                """,
                {"q": row[0], "id": listing_id, "limit": limit},
            ).fetchall()
        return [Hit(*r) for r in rows]
