"""Indexovanie: stiahni inzeráty → vyrob vektory iba pre nové a zmenené → ulož → zmaž zaniknuté."""

from dataclasses import dataclass

from ai_service.embeddings import Embedder
from ai_service.listings_client import ListingsSource
from ai_service.repository import EmbeddingRepository, StoredEmbedding


@dataclass(frozen=True)
class IndexResult:
    indexed: int  # nové alebo zmenené inzeráty – vektor sa počítal
    unchanged: int  # text aj model rovnaký – vektor ostal
    removed: int  # inzeráty, ktoré už nie sú verejné


def reindex(source: ListingsSource, embedder: Embedder, repository: EmbeddingRepository) -> IndexResult:
    documents = source.fetch_all()
    stored_hashes = repository.content_hashes()

    # Model je najdrahšia časť – počítame iba tam, kde sa text (alebo model) zmenil
    changed = [d for d in documents if stored_hashes.get(d.id) != d.content_hash(embedder.model_name)]

    if changed:
        vectors = embedder.embed([d.text() for d in changed])  # všetky naraz (v dávkach) – rýchlejšie
        repository.upsert(
            [
                StoredEmbedding(
                    d.id, d.title, d.status, d.content_hash(embedder.model_name), vector, d.search_text()
                )
                for d, vector in zip(changed, vectors, strict=True)
            ],
            embedder.model_name,
        )

    removed = repository.delete_except({d.id for d in documents})
    return IndexResult(indexed=len(changed), unchanged=len(documents) - len(changed), removed=removed)
