"""Vstupný bod služby: vytvorí FastAPI aplikáciu (obdoba Program.cs v C# API)."""

from fastapi import FastAPI
from pydantic import BaseModel

from ai_service import __version__


class HealthResponse(BaseModel):
    """Odpoveď /health. Pydantic model = record v C#: FastAPI z neho vyrobí JSON aj OpenAPI popis."""

    status: str
    service: str
    version: str


def create_app() -> FastAPI:
    """Továreň na aplikáciu – testy si tak vedia vytvoriť vlastnú, čistú inštanciu."""
    app = FastAPI(
        title="Vybavím to – AI service",
        version=__version__,
        description="Embeddings, semantic search and ML models for the Vybavím to marketplace.",
    )

    @app.get("/health", tags=["System"], summary="Health check")
    def health() -> HealthResponse:
        return HealthResponse(status="ok", service="ai-service", version=__version__)

    return app


# Inštancia, ktorú spúšťa server: `uvicorn ai_service.main:app`
app = create_app()
