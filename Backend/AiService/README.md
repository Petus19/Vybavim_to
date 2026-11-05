# AiService

A separate **Python (FastAPI)** service for the AI and machine-learning features of Vybavím to. It runs next to the C# API and has its own PostgreSQL schema (`ai`) – it never reads other modules' tables; listings come from the public REST API, like any other client.

| Step | Scope | Status |
|---|---|---|
| 1 – Foundation | FastAPI app, configuration, tests, lint, Docker image, CI | ✅ |
| 2 – Semantic search | multilingual sentence embeddings → pgvector (HNSW index); `/search`, `/listings/{id}/similar` | next |
| 3 – Evaluation | labelled query set; recall@5 and MRR of keyword search vs. semantic search | planned |
| 4 – Trained model | scikit-learn classifier on embeddings that suggests a listing category; train/test split, F1, confusion matrix | planned |
| 5 – Integration | the C# API calls the service; the main app keeps working when it is down | planned |

## Stack

Python 3.13 · FastAPI · Pydantic (settings and request/response models) · uvicorn · **uv** (Python version, virtual environment and locked dependencies) · pytest · Ruff (lint + format)

## Running locally

Requires [uv](https://docs.astral.sh/uv/). It uses an installed Python 3.13 or downloads one.

```bash
cd Backend/AiService
uv sync                                   # creates .venv from uv.lock
uv run uvicorn ai_service.main:app --reload --port 8000
#   → http://localhost:8000/docs           (interactive API docs)
```

## Tests and checks

```bash
uv run pytest           # tests
uv run ruff check .     # lint
uv run ruff format .    # format code
```

The same checks and a Docker build run in CI on every pull request.

## Docker

```bash
docker build -t vybavim-ai-service .
docker run --rm -p 8000:8000 vybavim-ai-service
```

Multi-stage build: dependencies are installed with uv from `uv.lock`, the final image contains only Python, the virtual environment and the code, and runs as a non-root user.

## Configuration

Environment variables with the `AI_` prefix (or a local `.env` file, never committed):

| Variable | Default | Purpose |
|---|---|---|
| `AI_API_BASE_URL` | `http://localhost:5080` | the main C# API – source of listings |
| `AI_DATABASE_URL` | local PostgreSQL from `docker-compose.yml` | the service's own `ai` schema (embeddings) |

## Layout

```
AiService/
  pyproject.toml        project, dependencies, Ruff and pytest settings
  uv.lock               exact versions of all packages
  src/ai_service/
    main.py             creates the FastAPI app (create_app) – like Program.cs
    config.py           settings from environment variables – like appsettings.json
  tests/                pytest; TestClient calls the app in memory
  Dockerfile
```
