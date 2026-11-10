"""Vstupný bod služby: vytvorí FastAPI aplikáciu (obdoba Program.cs v C# API)."""

from collections.abc import AsyncIterator, Callable
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Annotated
from uuid import UUID

from fastapi import Depends, FastAPI, HTTPException, Query, Request
from pydantic import BaseModel

from ai_service import __version__
from ai_service.config import Settings, get_settings
from ai_service.embeddings import Embedder, SentenceTransformerEmbedder
from ai_service.hybrid import DEFAULT_MIN_SCORE, HybridHit, HybridSearch
from ai_service.indexer import IndexResult, reindex
from ai_service.listings_client import ApiListingsSource, ListingsSource
from ai_service.repository import EmbeddingRepository, Hit, PostgresEmbeddingRepository

# --- Služby, ktoré endpointy potrebujú (obdoba registrácie v Dependency Injection v C#) ---


@dataclass
class Services:
    embedder: Embedder
    repository: EmbeddingRepository
    listings: ListingsSource
    min_score: float = DEFAULT_MIN_SCORE
    search: HybridSearch = field(init=False)  # skladá sa z embedderu a úložiska

    def __post_init__(self) -> None:
        self.search = HybridSearch(self.embedder, self.repository)


def build_services(settings: Settings) -> Services:
    """Skutočné služby: model, PostgreSQL a C# API. Testy podstrčia vlastnú továreň s falošnými."""
    embedder = SentenceTransformerEmbedder(settings.embedding_model)
    return Services(
        embedder=embedder,
        repository=PostgresEmbeddingRepository(settings.database_url, embedder.dimension),
        listings=ApiListingsSource(settings.api_base_url),
        min_score=settings.min_score,
    )


def get_services(request: Request) -> Services:
    return request.app.state.services


ServicesDep = Annotated[Services, Depends(get_services)]


# --- Tvary odpovedí (Pydantic modely = record v C#) ---


class HealthResponse(BaseModel):
    status: str
    service: str
    version: str


class IndexResponse(BaseModel):
    indexed: int
    unchanged: int
    removed: int


class SimilarHit(BaseModel):
    listing_id: UUID
    title: str
    status: str
    score: float  # kosínusová podobnosť, 1 = rovnaký význam

    @classmethod
    def from_hit(cls, hit: Hit) -> "SimilarHit":
        return cls(listing_id=hit.listing_id, title=hit.title, status=hit.status, score=round(hit.score, 4))


class SearchHit(BaseModel):
    listing_id: UUID
    title: str
    status: str
    score: float | None  # kosínusová podobnosť; None = nájdené iba podľa slov
    keyword_match: bool  # inzerát obsahuje všetky hľadané slová

    @classmethod
    def from_hit(cls, hit: HybridHit) -> "SearchHit":
        score = None if hit.semantic_score is None else round(hit.semantic_score, 4)
        return cls(
            listing_id=hit.listing_id,
            title=hit.title,
            status=hit.status,
            score=score,
            keyword_match=hit.keyword_match,
        )


def create_app(services_factory: Callable[[Settings], Services] = build_services) -> FastAPI:
    """Továreň na aplikáciu – testy si tak vedia vytvoriť vlastnú inštanciu s falošnými službami."""

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        # Raz pri štarte: načítaj model do pamäte (pár sekúnd) a priprav tabuľku v databáze.
        services = services_factory(get_settings())
        services.repository.ensure_schema()
        services.search.refresh()  # BM25 index z textov, ktoré už sú v databáze
        app.state.services = services
        yield

    app = FastAPI(
        title="Vybavím to – AI service",
        version=__version__,
        description="Embeddings, semantic search and ML models for the Vybavím to marketplace.",
        lifespan=lifespan,
    )

    @app.get("/health", tags=["System"], summary="Health check")
    def health() -> HealthResponse:
        return HealthResponse(status="ok", service="ai-service", version=__version__)

    @app.post("/index", tags=["Semantic search"], summary="(Re)index all public listings from the main API")
    def index(services: ServicesDep) -> IndexResponse:
        result: IndexResult = reindex(services.listings, services.embedder, services.repository)
        services.search.refresh()
        return IndexResponse(indexed=result.indexed, unchanged=result.unchanged, removed=result.removed)

    @app.get(
        "/search",
        tags=["Semantic search"],
        summary="Hybrid search: meaning (embeddings) + words (BM25); empty list = nothing relevant",
    )
    def search(
        services: ServicesDep,
        q: Annotated[str, Query(min_length=2, max_length=500, description="What the user is looking for")],
        limit: Annotated[int, Query(ge=1, le=50)] = 10,
        min_score: Annotated[
            float | None,
            Query(ge=0, le=1, description="Minimum cosine similarity; default from settings (0.45)"),
        ] = None,
    ) -> list[SearchHit]:
        threshold = services.min_score if min_score is None else min_score
        return [SearchHit.from_hit(h) for h in services.search.search(q, limit, threshold)]

    @app.get(
        "/listings/{listing_id}/similar",
        tags=["Semantic search"],
        summary="Listings most similar to the given one",
        responses={404: {"description": "Listing is not indexed"}},
    )
    def similar(
        services: ServicesDep,
        listing_id: UUID,
        limit: Annotated[int, Query(ge=1, le=20)] = 5,
    ) -> list[SimilarHit]:
        hits = services.repository.similar(listing_id, limit)
        if hits is None:
            raise HTTPException(status_code=404, detail="Listing is not indexed.")
        return [SimilarHit.from_hit(h) for h in hits]

    return app


# Inštancia, ktorú spúšťa server: `uvicorn ai_service.main:app`
app = create_app()
