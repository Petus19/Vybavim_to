# AiService

A separate **Python (FastAPI)** service for the AI and machine-learning features of Vybavím to. It runs next to the C# API and has its own PostgreSQL schema (`ai`) – it never reads other modules' tables; listings come from the public REST API, like any other client.

| Step | Scope | Status |
|---|---|---|
| 1 – Foundation | FastAPI app, configuration, tests, lint, Docker image, CI | ✅ |
| 2 – Semantic search | multilingual sentence embeddings → pgvector (HNSW index); `/index`, `/search`, `/listings/{id}/similar` | ✅ |
| 3 – Evaluation | labelled query set; recall@5 and MRR of keyword search, BM25, two embedding models and a hybrid | ✅ |
| 3b – Hybrid search | `/search` serves BM25 + embeddings (Reciprocal Rank Fusion) with a minimum score, so it can say "nothing relevant" | ✅ |
| 4 – Trained model | scikit-learn classifier on embeddings that suggests a listing category; train/test split, F1, confusion matrix | next |
| 5 – Integration | the C# API calls the service; the main app keeps working when it is down | planned |

## Stack

Python 3.13 · FastAPI · Pydantic (settings and request/response models) · uvicorn · **sentence-transformers** + PyTorch (CPU) · **PostgreSQL + pgvector** (psycopg 3) · **uv** (Python version, virtual environment and locked dependencies) · pytest + Testcontainers · Ruff (lint + format)

## Semantic search – how it works

```
POST /index   C# API (all public listings) → text → embedding model → vector(384) → ai.listing_embeddings
GET  /search?q=…              hybrid: nearest vectors in pgvector (cosine) + BM25 over the same listings,
                              merged with Reciprocal Rank Fusion, filtered by a minimum score
GET  /listings/{id}/similar   nearest vectors to that listing's vector
```

- **Model:** [`paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2) – a pretrained multilingual sentence-transformer (50+ languages incl. Slovak), 384-dimensional vectors, runs on CPU. Compared with `multilingual-e5-small` in the [evaluation](#evaluation): same recall, e5 ranks the first hit slightly higher, but only this model's scores separate "good match" from "nothing relevant" well enough for a minimum-score cut-off.
- **Text per listing:** title, category, description and skills (`listing_text.py`).
- **Vectors are normalized** (length 1), so cosine similarity = dot product; pgvector's `<=>` operator returns cosine distance and the result `score` is `1 − distance`.
- **HNSW index** (`vector_cosine_ops`) finds nearest neighbours without comparing against every row.
- **Incremental indexing:** each row stores a SHA-256 of the text + model name + index format version; `/index` only re-embeds new or changed listings and deletes vectors of listings that are no longer public. Second run on unchanged data: `{"indexed": 0, "unchanged": 12, "removed": 0}`.
- **Data access:** listings come from the public REST API (list + detail), never from the Listings tables. The service owns only the `ai` schema, created at start-up (`CREATE … IF NOT EXISTS`).

First results on the 12 demo listings (score = cosine similarity):

| Query | Top result | |
|---|---|---|
| *potrebujem niekoho kto mi navrhne znak firmy* ("someone to design a company sign") | Logo a vizuálna identita pre kaviareň – 0.46 | ✅ no shared words |
| *nefunguje mi wifi v kancelárii* | Wi-Fi v predajni 0.30, Správa siete v kancelárii 0.29 | ✅ both network jobs on top |
| *chcem predávať cez internet* ("I want to sell online") | E-shop s domácou kozmetikou – 0.50 | ✅ |
| *webstránka pre penzión* ("website for a guesthouse") | Landing page for a fitness app 0.45; the guesthouse SEO job only 3rd | ⚠️ |
| *niečo s účtovníctvom* ("something with accounting") | best score 0.26 – nothing relevant exists | ⚠️ low scores = no good match |

The weak cases were the reason for step 3: a labelled query set and metrics instead of impressions, and then for the hybrid search below.

## Hybrid search (`GET /search`)

BM25 (`text_search.py`) and the embeddings (`hybrid.py`) are merged with Reciprocal Rank Fusion and then filtered. A result is kept when:

1. its **meaning is close enough** – cosine ≥ `min_score` (default **0.45**), or
2. it contains **all query words** – e.g. "SEO" or "WooCommerce", where the model is unsure, or
3. it contains **some query words and the meaning is at least weakly related** – cosine ≥ 0.25 ("nefunguje mi wifi" → the Wi-Fi job at 0.27).

Only partly matching words with unrelated meaning are dropped ("oprava práčky" – washing-machine repair – must not return "Oprava chýb na webe"). Results from rules 1–2 are listed before rule 3. **An empty list means nothing relevant** – which the agent relies on to not invent listings.

BM25 details: words without diacritics, cut to 5 characters and one final vowel dropped as a crude Slovak stemmer, hyphens joined ("Wi-Fi" = "wifi"), numbers and filler words ("chcem", "potrebujem", "pre", "kto", "eur" …) ignored. The index is rebuilt in memory from the `ai` table after start-up and after every `/index` (milliseconds for hundreds of listings).

Response: `[{listing_id, title, status, score, keyword_match}]` – `score` is the cosine similarity (`null` if found only by words), `keyword_match` = all query words present. `min_score` can be overridden per request (`?min_score=0` returns everything ranked).

## Evaluation

```bash
uv run python -m ai_service.evaluation                       # the service's model
uv run python -m ai_service.evaluation --model A --model B   # compare embedding models
```

**Test set** (`src/ai_service/evaluation/data/`): 60 listings, 15 in each of the 4 categories, and 43 Slovak queries with hand-labelled correct listings. Queries are grouped by kind – `exact` words from the listing, `synonym` (different words, same meaning), `no-diacritics`, `typo`, `natural` (a whole sentence) and 6 `no-answer` queries with no correct listing. **The data is synthetic** – written for this project, not collected from users.

**Methods compared:**
- **keyword** – a faithful copy of the current C# API search: the whole query as one substring of title + description + skills, ignoring case and diacritics (`ILIKE` + `unaccent`), no relevance ranking;
- **BM25** – classic full-text ranking (as in Lucene / Elasticsearch), with words cut to their first 5 characters and one final vowel dropped as a crude Slovak stemmer ("logo" = "loga" = "logá") – a deliberately strong non-AI baseline;
- **semantic** – cosine similarity of embeddings, the same text and maths as the service (exact search in memory instead of HNSW);
- **hybrid** – BM25 and semantic rankings merged with Reciprocal Rank Fusion (`1 / (60 + rank)` summed over both lists), no filter;
- **hybrid as served** – exactly what `GET /search` returns: the hybrid plus the relevance rules above (same code: `hybrid.fuse`).

**Metrics:** **recall@5** – share of correct listings among the first 5 results; **MRR** – mean of `1 / position` of the first correct result (1st → 1, 2nd → 0.5, not in top 10 → 0). Per-kind columns are recall@5. **No-answer → empty** – share of the 6 no-answer queries for which the method honestly returns nothing.

| Method | recall@5 | MRR | exact | synonym | no-diacritics | typo | natural | no-answer → empty |
|---|---|---|---|---|---|---|---|---|
| keyword (current C# API) | **0.24** | 0.27 | 0.86 | 0.00 | 0.75 | 0.00 | 0.00 | 100% |
| BM25 | **0.87** | 0.81 | 0.93 | 0.77 | 1.00 | 1.00 | 0.90 | 67% |
| semantic – paraphrase-multilingual-MiniLM-L12-v2 (used) | **0.86** | 0.85 | 0.76 | 0.90 | 1.00 | 0.75 | 0.80 | 0% |
| semantic – multilingual-e5-small | **0.86** | 0.94 | 0.89 | 0.76 | 1.00 | 1.00 | 0.90 | 0% |
| hybrid – BM25 + MiniLM | **0.92** | 0.89 | 1.00 | 0.85 | 1.00 | 1.00 | 0.90 | 0% |
| hybrid – BM25 + e5 | **0.91** | 0.90 | 0.93 | 0.86 | 1.00 | 1.00 | 0.90 | 0% |
| **hybrid as served** (MiniLM, min. score 0.45) | **0.91** | **0.91** | 0.93 | 0.90 | 1.00 | 1.00 | 0.80 | **83%** |

**Minimum score for pure semantic search** – it always returns *something*. Share of queries kept / rejected when results below a cut-off are hidden:

| MiniLM cut-off | answerable queries still answered | no-answer queries rejected | | e5 cut-off | answered | rejected |
|---|---|---|---|---|---|---|
| 0.32 | 95% | 50% | | 0.828 | 100% | 17% |
| **0.41** | **92%** | **83%** | | 0.843 | 89% | 67% |
| 0.50 | 73% | 100% | | 0.857 | 65% | 83% |

**What it shows:**
- The current keyword search finds exact words but **nothing** for synonyms, typos or whole sentences – recall@5 0.24.
- Even without AI, BM25 with a crude stemmer reaches 0.87 – a fair baseline matters; semantic search is better mainly on synonyms (0.90 vs 0.77) and finds the first correct result higher (MRR 0.85 vs 0.81).
- The **hybrid is best (0.92)**: BM25 catches exact words and product names, embeddings catch meaning.
- **The served hybrid keeps almost all of it (0.91, MRR 0.91) and can say "nothing relevant"** for 5 of 6 no-answer queries. Because the word path rescues exact matches, the semantic cut-off can be stricter (0.45) than for semantic search alone (0.41).
- e5 ranks better (MRR 0.94), but its scores sit in a narrow 0.81–0.90 band for both good and irrelevant queries, so a cut-off barely works. MiniLM separates them – so it stays the default.
- **Caveats:** 37 answerable queries – one query moves recall by ~0.03, so differences of a few hundredths are noise; the same person wrote listings, queries and labels; the cut-offs were chosen on this same set (and checked on the 12 demo listings), so they are tuned, not independently validated.

## Running locally

Requires [uv](https://docs.astral.sh/uv/) (it uses an installed Python 3.13 or downloads one), the database from `docker-compose.yml` and the C# API running.

```bash
cd Backend/AiService
uv sync                                   # creates .venv from uv.lock
uv run uvicorn ai_service.main:app --reload --port 8000
#   → http://localhost:8000/docs           (interactive API docs)
```

The first start downloads the model (~470 MB) into the Hugging Face cache; later starts load it in a few seconds. Then call `POST /index` once and try `GET /search?q=…` in `/docs`.

## Tests and checks

```bash
uv run pytest           # tests (Docker must be running – repository tests use a throw-away pgvector container)
uv run pytest -m model  # also the test with the real model (large download, skipped by default)
uv run ruff check .     # lint
uv run ruff format .    # format code
```

| Test file | What it checks |
|---|---|
| `test_search_api.py` | `/index`, `/search`, `/similar` over HTTP with a fake embedder and in-memory storage: incremental indexing, ranking, minimum score and "nothing relevant", word-only matches, limits, validation (422), 404 |
| `test_text_search.py` | BM25: word forms, filler words, hyphens and numbers, partial vs full match; Reciprocal Rank Fusion |
| `test_hybrid.py` | the relevance rules of the hybrid search and the order of strong vs weak results |
| `test_repository.py` | the real SQL against PostgreSQL + pgvector (Testcontainers): cosine ordering, upsert, similar, delete |
| `test_listings_client.py` | paging through the C# API and reading details (mocked HTTP) |
| `test_listing_text.py` | listing text and content hash |
| `test_model.py` | the real model puts "logo" closer to "visual identity" than to "network repair" (`-m model`) |
| `test_evaluation_metrics.py` | recall@k and reciprocal rank on hand-computed examples |
| `test_retrievers.py` | keyword search behaves like the C# API, BM25, semantic ranking, hybrid with and without the filter |
| `test_evaluation.py` | test-set consistency (labels point to existing listings, balanced categories) and the evaluation run |

The same checks (without the model test) and a Docker build run in CI on every pull request.

## Docker

```bash
docker build -t vybavim-ai-service .
docker run --rm -p 8000:8000 -e AI_API_BASE_URL=http://host.docker.internal:5080 -e AI_DATABASE_URL=postgresql://vybavim:vybavim@host.docker.internal:5432/vybavim vybavim-ai-service
```

Multi-stage build: dependencies are installed with uv from `uv.lock`, the final image contains only Python, the virtual environment and the code, and runs as a non-root user. PyTorch comes from the **CPU-only** index (`[tool.uv.sources]` in `pyproject.toml`): the image is ~2.1 GB instead of ~7 GB with the default CUDA build.

## Configuration

Environment variables with the `AI_` prefix (or a local `.env` file, never committed):

| Variable | Default | Purpose |
|---|---|---|
| `AI_API_BASE_URL` | `http://localhost:5080` | the main C# API – source of listings |
| `AI_DATABASE_URL` | local PostgreSQL from `docker-compose.yml` | the service's own `ai` schema (embeddings) |
| `AI_EMBEDDING_MODEL` | `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` | embedding model; a model with a different vector size needs the `ai.listing_embeddings` table recreated |
| `AI_MIN_SCORE` | `0.45` | minimum cosine similarity for a hybrid search result found by meaning; re-measure it when changing the model |

## Layout

```
AiService/
  pyproject.toml          project, dependencies, Ruff and pytest settings
  uv.lock                 exact versions of all packages
  src/ai_service/
    main.py               FastAPI app, start-up (lifespan), endpoints – like Program.cs
    config.py             settings from environment variables – like appsettings.json
    embeddings.py         Embedder protocol + sentence-transformers implementation
    listing_text.py       listing → text for the model, content hash
    listings_client.py    reads listings from the C# API
    repository.py         pgvector storage and nearest-neighbour queries
    indexer.py            incremental (re)indexing
    text_search.py        BM25 index and Reciprocal Rank Fusion (shared by the service and the evaluation)
    hybrid.py             hybrid search: relevance rules (fuse) and HybridSearch used by GET /search
    evaluation/           test set (data/*.json), metrics, keyword/BM25/semantic/hybrid retrievers, report
  tests/                  pytest; fakes.py = fake model, storage and listings for fast API tests
  Dockerfile
```
