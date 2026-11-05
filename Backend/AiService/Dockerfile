# Docker obraz AI služby. Dve fázy (multi-stage):
#   1. "build" – uv nainštaluje závislosti do .venv podľa uv.lock (presné verzie)
#   2. "runtime" – iba Python + hotové .venv + kód, bez uv a bez vývojových balíčkov → menší obraz

FROM python:3.13-slim AS build
COPY --from=ghcr.io/astral-sh/uv:0.13.7 /uv /bin/uv
WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy

# Najprv iba závislosti – táto vrstva sa pri zmene kódu nemusí stavať znova (rýchlejší build)
COPY pyproject.toml uv.lock ./
RUN uv sync --locked --no-dev --no-install-project

# Potom samotný kód služby
COPY README.md ./
COPY src ./src
RUN uv sync --locked --no-dev

FROM python:3.13-slim AS runtime
WORKDIR /app
# Služba nebeží ako root – menšie riziko, keby ju niekto zneužil
RUN useradd --create-home appuser
COPY --from=build --chown=appuser /app /app
USER appuser
ENV PATH="/app/.venv/bin:$PATH" PYTHONUNBUFFERED=1
EXPOSE 8000
CMD ["uvicorn", "ai_service.main:app", "--host", "0.0.0.0", "--port", "8000"]
