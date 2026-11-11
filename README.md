# Vybavím to

[![CI](https://github.com/Petus19/Vybavim_to/actions/workflows/ci.yml/badge.svg)](https://github.com/Petus19/Vybavim_to/actions/workflows/ci.yml)

**A two-sided service marketplace** – people and small businesses post a job ("logo for my café", "office network support"), and freelancers or agencies respond. The first target market is IT and graphic design in Slovakia (hence the Slovak UI; *Vybavím to* means *"I'll get it done"*).

> **Status:** work in progress (started October 2026). Browsing and search, accounts (email/password + Google) and posting/managing listings work end to end (web + API + database). Profiles and **offers on listings** (send, accept → listing moves to in-progress, reject, withdraw) are done in the API; their web pages are next. A separate **Python AI service** already does semantic search, with a measured improvement over keyword search. Then projects, chat and reviews – see [Roadmap](#roadmap).

| Listings with search and filters | Listing detail |
|---|---|
| ![Listings](docs/screenshots/listings.png) | ![Listing detail](docs/screenshots/listing-detail.png) |
| **Home page** | **Interactive API documentation (OpenAPI + Scalar)** |
| ![Home](docs/screenshots/home.png) | ![API docs](docs/screenshots/api-docs.png) |

---

## What this project shows

- **Backend architecture** – a *modular monolith* in C# / ASP.NET Core (.NET 10): each business module owns its code, its database schema and a small public contract. Module boundaries are enforced by the compiler **and** by automated architecture tests.
- **Domain modelling** – entities protect their own rules (factory methods, private setters, explicit state transitions); every broken rule has a stable error code and a human-readable message.
- **A real REST API** – accent- and case-insensitive text search with filters, paging, authentication with access/refresh tokens and Google sign-in, role-based authorization, RFC 9457 error responses, OpenAPI docs.
- **Testing and CI** – 126 automated C# tests: unit, architecture, and integration tests that run the whole API against a throw-away PostgreSQL in Docker (Testcontainers), plus 58 Python tests for the AI service. GitHub Actions builds and tests the backend, the AI service (including its Docker image) and the frontend on every pull request.
- **AI service (Python)** – a FastAPI service next to the C# API. Multilingual sentence embeddings stored in PostgreSQL with pgvector (HNSW) power semantic search and "similar listings": a query like "someone to design a company sign" finds the logo job with no words in common. **Measured, not assumed:** on a labelled set of 43 Slovak queries, recall@5 goes from **0.24** (current keyword search) to **0.86** (semantic) and **0.91** (hybrid with BM25, which can also answer "nothing relevant"); the evaluation also decided which embedding model to keep. See [`Backend/AiService`](Backend/AiService/README.md).
- **Teamwork** – two developers, every change goes through a branch, a descriptive pull request and a green CI run. The web client consumes TypeScript types generated from the API's OpenAPI document.

## Team

| | Role |
|---|---|
| **Petus19** | Backend (C#, database, API, authentication, tests, CI) |
| **Samuel Chovanec** | Frontend (Next.js), Docker development environment |

> **Language note:** the product targets Slovak users, so the UI, code comments and commit messages are in Slovak (the team's working language). Identifiers, API contracts and this documentation are in English.

---

## Architecture

```mermaid
flowchart LR
    web["Web<br/>Next.js 16 · TypeScript"] -->|"REST /api/v1 · bearer tokens"| api
    subgraph api["API – ASP.NET Core (.NET 10), modular monolith"]
        identity["Identity<br/>accounts, tokens, roles"]
        profiles["Profiles"]
        projects["Projects<br/>offers"]
        listings["Listings<br/>search, CRUD"]
        listings -. "IProfilesApi<br/>(Profiles.Contracts)" .-> profiles
        projects -. "IListingsApi" .-> listings
    end
    api --> db[("PostgreSQL 17<br/>schema per module")]
    web -. "OAuth 2.0 + PKCE" .-> google["Google sign-in"]
    ai["AI service<br/>Python · FastAPI<br/>embeddings, semantic search"] -->|"REST (public API only)"| api
    ai -->|"own schema ai<br/>pgvector"| db
```

```
Backend/
  Api/                 host: wires modules, OpenAPI, auth, error handling – no business logic
  Common/              shared technical code (DB base class, errors/Result, current user)
  Modules/<Module>/
    <Module>/          implementation – everything internal
      Endpoints/       HTTP endpoints (thin: call a use case, map the result)
      Application/     use cases (return Result<T>)
      Domain/          entities and business rules
      Infrastructure/  EF Core DbContext, mappings, migrations, dev seed data
    <Module>.Contracts/   the ONLY thing other modules may reference
  Tests/               unit, architecture and integration tests
  AiService/           Python AI service (FastAPI): semantic search, evaluation – own README
Frontend/
  Web/                 Next.js app (Slovak UI)
  Shared/              TypeScript API types generated from OpenAPI
```

**Rules that keep it maintainable**

1. A module may reference another module **only through its `.Contracts` project** (e.g. Listings asks Profiles for the listing author via `IProfilesApi`). No circular dependencies, no reaching into another module's tables.
2. Everything inside a module is `internal`; the only public type is `{Module}Module` (`AddListingsModule()`, `MapListingsEndpoints()`).
3. Each module has its **own PostgreSQL schema** and migration history (`identity`, `profiles`, `listings`, `projects`).
4. Expected errors are values (`Error` + `Result<T>`), mapped to `400/401/403/404/409` ProblemDetails with a `code` field the web uses to highlight form fields.
5. Rules 1–3 are checked by **architecture tests** (reflection over the compiled assemblies).

Modules that exist as empty skeletons for later phases: Chat, Notifications, Reviews, Payments, Admin, Gdpr.

### Where to look in the code

| What | File |
|---|---|
| Domain entity with rules and state transitions | [`Modules/Listings/Listings/Domain/Listing.cs`](Backend/Modules/Listings/Listings/Domain/Listing.cs) |
| Search query (filters → one SQL statement) | [`Modules/Listings/Listings/Application/SearchListings.cs`](Backend/Modules/Listings/Listings/Application/SearchListings.cs) |
| Cross-module contract and its use | [`Profiles.Contracts/IProfilesApi.cs`](Backend/Modules/Profiles/Profiles.Contracts/IProfilesApi.cs), [`GetListingDetail.cs`](Backend/Modules/Listings/Listings/Application/GetListingDetail.cs) |
| Business process across two modules (offer accepted → listing in progress) | [`Modules/Projects/Projects/Application/AcceptOffer.cs`](Backend/Modules/Projects/Projects/Application/AcceptOffer.cs) |
| Authentication use case | [`Modules/Identity/Identity/Application/LoginUser.cs`](Backend/Modules/Identity/Identity/Application/LoginUser.cs) |
| Architecture tests | [`Tests/ArchitectureTests/ModuleBoundaryTests.cs`](Backend/Tests/ArchitectureTests/ModuleBoundaryTests.cs) |
| Integration tests (API + real PostgreSQL) | [`Tests/IntegrationTests/ListingsManageTests.cs`](Backend/Tests/IntegrationTests/ListingsManageTests.cs) |
| Composition root | [`Api/Program.cs`](Backend/Api/Program.cs) |
| AI: incremental indexing and vector search (pgvector) | [`AiService/src/ai_service/indexer.py`](Backend/AiService/src/ai_service/indexer.py), [`repository.py`](Backend/AiService/src/ai_service/repository.py) |
| AI: search evaluation (keyword vs BM25 vs semantic vs hybrid) | [`AiService/src/ai_service/evaluation/`](Backend/AiService/src/ai_service/evaluation/) |

## Tech stack

| Area | Technology |
|---|---|
| Backend | C#, .NET 10, ASP.NET Core Minimal APIs, ASP.NET Core Identity, EF Core + Npgsql |
| Database | PostgreSQL 17 (`unaccent` text search, pgvector for embeddings; PostGIS available in the image) |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4, shadcn/ui, openapi-fetch |
| Auth | Bearer access/refresh tokens, roles, Google sign-in (OAuth 2.0 + PKCE), lockout after failed logins |
| API docs | OpenAPI + Scalar UI |
| Testing | xUnit v3 (Microsoft.Testing.Platform), Testcontainers, WebApplicationFactory |
| Dev environment | Docker Compose: PostgreSQL, Redis, S3-compatible storage (RustFS), Mailpit |
| AI service | Python 3.13, FastAPI, Pydantic, uv, sentence-transformers (PyTorch, CPU), pgvector, pytest + Testcontainers, Ruff |
| CI | GitHub Actions |

---

## Getting started

**Prerequisites:** [Docker](https://www.docker.com/products/docker-desktop/) (running), [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Node.js 24](https://nodejs.org/) – works on Windows, macOS and Linux.

```bash
git clone https://github.com/Petus19/Vybavim_to.git
cd Vybavim_to

# 1. Infrastructure: PostgreSQL, Redis, storage, mail catcher
docker compose up -d

# 2. API – applies database migrations and loads demo data on first start
cd Backend
dotnet run --project Api
#    → http://localhost:5080/scalar   (interactive API docs)
```

In a second terminal:

```bash
# 3. Web
cd Frontend/Web
npm install
npm run dev
#    → http://localhost:3000
```

**AI service (optional)** – needs [uv](https://docs.astral.sh/uv/) and the API running; the first start downloads the embedding model (~470 MB):

```bash
cd Backend/AiService
uv sync
uv run uvicorn ai_service.main:app --port 8000
#    → http://localhost:8000/docs   (call POST /index once, then try GET /search)
```

**Demo data:** 4 categories, 62 listings and their authors' profiles are created automatically in development. Development-only accounts are seeded as well (never created outside the Development environment):

| User | Password | Role in the demo |
|---|---|---|
| `Admin` | `Admin123` | administrator |
| `client.demo` | `Demo1234` | a car repair shop in Košice with 3 listings and 4 offers on them |
| `specialist.demo` | `Demo1234` | graphic designer (offer on the logo redesign) |
| `specialist2.demo` | `Demo1234` | junior web & graphic designer (cheaper competing offers) |
| `specialist3.demo` | `Demo1234` | performance-marketing agency (Google Ads offer) |

The demo accounts and the extra 50 listings (one of them carries a planted prompt-injection text for the agent's safety scenario) (switch `DevData:DemoAccounts` in `appsettings.Development.json`) exist for the AI agent and its evaluation. You can also register a new account on the web.

**Google sign-in is optional** – the button appears only when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `Frontend/Web/.env.local` (see `.env.example`). Everything else works without it.

### Running the tests

```bash
cd Backend
dotnet test        # 126 tests; Docker must be running (integration tests start their own PostgreSQL)
```

```bash
cd Frontend/Web
npm run lint && npm run typecheck && npm run build
```

```bash
cd Backend/AiService
uv run pytest                          # 58 tests; Docker must be running (pgvector container)
uv run python -m ai_service.evaluation # search quality: recall@5 and MRR per method
```

### Troubleshooting

| Problem | Fix |
|---|---|
| API fails on start with a connection error | Docker is not running, or run `docker compose up -d` in the repository root |
| Port already in use (5432, 6379, …) | copy `.env.example` to `.env` and change the port; for the database port also update `Backend/Api/appsettings.Development.json` |
| Web shows "Niečo sa nepodarilo" (*something went wrong*) | the API is not running on `http://localhost:5080` |

---

## API overview

Base URL `http://localhost:5080/api/v1` · full reference in [`Backend/Api/README.md`](Backend/Api/README.md) and at `/scalar`.

| Method | Endpoint | Access | Purpose |
|---|---|---|---|
| POST | `/auth/register`, `/auth/login`, `/auth/google`, `/auth/refresh` | public | accounts and tokens |
| GET | `/auth/me` | signed in | current user and roles |
| GET | `/users` | admin | user list |
| GET / PUT | `/profiles/me` | signed in | my profile (create or update) |
| GET | `/profiles/{userId}` | public | public profile |
| GET | `/categories` | public | categories |
| GET | `/listings` | public | search: text, category, city, price type and ranges, level, remote, sort, paging |
| GET | `/listings/{id}` | public | detail including the author's profile |
| POST / PUT | `/listings`, `/listings/{id}` | signed in / author | create, edit |
| POST | `/listings/{id}/cancel` | author or admin | withdraw a listing |
| GET | `/listings/mine` | signed in | my listings |
| POST / GET | `/listings/{id}/offers` | signed in / listing author | send an offer / see offers on my listing |
| GET | `/offers/mine` | signed in | my sent offers |
| POST | `/offers/{id}/accept`, `/reject`, `/withdraw` | listing author / offer author | decide on an offer |

---

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 – Foundation | architecture, Docker, database, search API, tests, CI | ✅ done |
| 1 – MVP | accounts ✅, Google sign-in ✅, profiles ✅, posting listings ✅, offers on listings ✅ (API), **choosing a specialist → project**, chat, reviews, email notifications, GDPR tools | in progress |
| 2 – Monetization & AI | Stripe subscriptions; AI features in a separate **Python (FastAPI) service** – foundation ✅, semantic search ✅, search evaluation ✅; next: category suggestion (trained scikit-learn classifier), using the service from the C# API and web, listing-writing assistant (LLM), content moderation | AI in progress |
| 3 – Growth | mobile app (Expo) on the same API, escrow payments, matching/recommendations and fraud detection (ML) | planned |

The AI service never reads other modules' tables: it calls the public REST API (later it will also subscribe to domain events), so it has the same permissions as a regular user. It keeps only its own `ai` schema for embeddings.

The original technical design document (Slovak) is in [`docs/technical-design.sk.md`](docs/technical-design.sk.md).
