"""Testy cez HTTP bez spusteného servera – TestClient volá aplikáciu priamo v pamäti
(obdoba WebApplicationFactory v C# integračných testoch)."""

from fastapi.testclient import TestClient

from ai_service.main import create_app


def test_health_returns_ok() -> None:
    client = TestClient(create_app())

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "service": "ai-service", "version": "0.1.0"}


def test_openapi_document_is_available() -> None:
    client = TestClient(create_app())

    response = client.get("/openapi.json")

    assert response.status_code == 200
    assert "/health" in response.json()["paths"]
