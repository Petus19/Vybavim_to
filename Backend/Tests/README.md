# Tests

95 automated tests in four projects:

| Project | What it tests | Needs |
|---|---|---|
| `Modules.Listings.Tests` | listing rules (`Listing.Create`, `Update`, state transitions) – fast, no database | nothing |
| `Modules.Profiles.Tests` | profile rules (`Profile.Create`, `Update`, website validation) | nothing |
| `ArchitectureTests` | structural rules: modules reference each other only through `.Contracts`, `Common` and `.Contracts` know no module, module internals stay `internal`, `Domain` does not depend on EF Core or HTTP | nothing |
| `IntegrationTests` | the whole API over HTTP against a real PostgreSQL: search and filters, authentication and roles, creating/editing/withdrawing listings, profiles, error responses | **Docker running** |

## Running

In the `Backend` folder:

```bash
dotnet test
```

A single project:

```bash
dotnet test --project Tests/Modules.Listings.Tests
```

The tests use **xUnit v3** on **Microsoft.Testing.Platform** (enabled in `Backend/global.json`).

## Integration tests

`ApiFactory` starts the API in memory (`WebApplicationFactory`) and uses **Testcontainers** to start a throw-away PostgreSQL in Docker (same version as `docker-compose.yml`). On start-up the API applies migrations and loads the demo data, so the tests always see the same 12 listings and 4 categories. The container is removed afterwards – the development database is never touched.

- The API is started once in `ApiFactory.InitializeAsync`, before any test runs. Test classes run in parallel; starting the API lazily from several of them at once would run migrations concurrently.
- Tests that create listings use `WritableApiFactory` with **its own database**, so they cannot disturb the search tests that expect exactly 12 listings.
- Every test that needs a user registers a fresh one with a unique email.

## Adding tests

- **a domain rule** → `Tests/Modules.<Module>.Tests/Domain/` – the project can see the module's `internal` types (`InternalsVisibleTo` in `Backend/Directory.Build.props`)
- **a new endpoint** → `Tests/IntegrationTests/`
- **a new module** → a test project `Tests/Modules.<Module>.Tests` + add it to `Vybavim.slnx`
- test names: `Method_Scenario_ExpectedResult`, e.g. `Create_NegotiableWithAmount_Throws`

## CI

`.github/workflows/ci.yml` runs on every pull request: backend restore, Release build (warnings as errors) and all tests; web lint, type check and production build.
