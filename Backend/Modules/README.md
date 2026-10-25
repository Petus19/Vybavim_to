# Modules

The modules of the modular monolith. Each module consists of **two projects**:

```
Listings/
├── Listings/                 # Vybavim.Modules.Listings – implementation, everything internal
│   ├── Endpoints/            # REST endpoints /api/v1/… (Minimal APIs)
│   ├── Application/          # use cases ("create a listing", "search listings")
│   ├── Domain/               # entities and business rules
│   └── Infrastructure/       # the module's DbContext (its own schema only), migrations, dev seed data
└── Listings.Contracts/       # Vybavim.Modules.Listings.Contracts – the contract for other modules:
                              # interfaces, DTOs, integration events
```

| Module | Status |
|---|---|
| `Identity` | ✅ accounts, password and Google sign-in, tokens, roles |
| `Profiles` | ✅ public profiles; exposes `IProfilesApi` in `Profiles.Contracts` |
| `Listings` | ✅ categories, search, create / update / withdraw listings |
| `Projects`, `Chat`, `Notifications`, `Reviews`, `Payments`, `Admin`, `Gdpr` | empty skeletons for later phases |

**Rules**

- A module references other modules **only through their `.Contracts` project**, never their implementation. This prevents circular dependencies (e.g. Projects ↔ Listings) and lets the compiler guard the boundary. Example: Listings shows the listing author by calling `IProfilesApi` from `Profiles.Contracts`.
- A `.Contracts` project references no other project.
- Implementation types are `internal`. The only public type is the module's registration class (`AddListingsModule()`, `MapListingsEndpoints()`), called by `Api`.
- Each module has its **own PostgreSQL schema** (`HasDefaultSchema("listings")`) and its own migration history in `Infrastructure/Migrations/`.
- Shared technical code (database, errors, current user) lives in [`Common/`](../Common/README.md), not in modules.
- Modules that work with everyone's data (`Gdpr`, `Admin`) will do so through interfaces in `.Contracts` – e.g. each module implements export and anonymisation of its personal data and `Gdpr` only calls them.

These rules are checked automatically by the architecture tests in [`Tests/ArchitectureTests`](../Tests/ArchitectureTests).

**Search** is part of `Listings` for the MVP. A separate `Search` module with its own read model is planned together with semantic search (pgvector).
