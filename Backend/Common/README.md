# Common

Shared **technical** code used by all modules, written once so modules don't duplicate it. No business logic lives here.

| Folder | Contents |
|---|---|
| `Database/` | `BaseDbContext` (schema per module, auto-discovered entity configurations, money as `numeric(12,2)`), `IModuleDbContext`, `DatabaseMigrator` (applies migrations on start-up in development), `IDevDataSeeder` (demo data, development only) |
| `Errors/` | `Error` (code + message + kind), `Result<T>`, `DomainException`, mapping to RFC 9457 ProblemDetails (`ToProblem()`) |
| `Auth/` | `ICurrentUser` (who is calling the API – modules use it without knowing anything about tokens), role names (`Roles.Admin`) |
| `Extensions/` | registration helpers: `AddModuleDbContext<T>()`, `MigrateDatabasesAsync()`, `SeedDevDataAsync()`, `AddCurrentUser()` |
| `Events/` | reserved for domain events and the transactional outbox (planned) |

> Business logic does **not** belong here. Entities such as `Listing` or `Profile` and their rules stay in their module. When a module needs another module's data, it uses that module's `.Contracts` project.
