# Tests

| Projekt | Čo testuje | Potrebuje |
|---|---|---|
| `Modules.Listings.Tests` | pravidlá inzerátu (`Listing.Create`, zmeny stavu) – rýchle, bez databázy | nič |
| `ArchitectureTests` | pravidlá štruktúry: moduly odkazujú na iné moduly iba cez `.Contracts`, `Common` a `.Contracts` nepoznajú moduly, vnútro modulu je `internal`, `Domain` nezávisí od databázy ani HTTP | nič |
| `IntegrationTests` | celé Api cez HTTP proti skutočnému PostgreSQL (endpointy, filtre, chyby) | **bežiaci Docker** |

## Spustenie

V priečinku `Backend`:

```bash
dotnet test
```

Iba jeden projekt:

```bash
dotnet test --project Tests/Modules.Listings.Tests
```

Testy bežia na **Microsoft.Testing.Platform** (zapnuté v `Backend/global.json`) a používajú **xUnit v3**.

## Integračné testy

`ApiFactory` spustí Api v pamäti testu a cez **Testcontainers** si naštartuje dočasný PostgreSQL v Dockeri (rovnaká verzia ako v `docker-compose.yml`). Api pri štarte aplikuje migrácie a vloží testovacie dáta, takže testy majú vždy rovnakých 12 inzerátov a 4 kategórie. Po testoch sa kontajner zmaže – vývojová databáza z `docker-compose` sa nemení.

## Ako pridať testy

- **pravidlo v doméne** → `Tests/Modules.<Modul>.Tests/Domain/` – projekt vidí `internal` typy modulu (`InternalsVisibleTo` v `Backend/Directory.Build.props`)
- **nový endpoint** → `Tests/IntegrationTests/`
- **nový modul** → testovací projekt `Tests/Modules.<Modul>.Tests` + pridať do `Vybavim.slnx`
- názvy testov: `Metóda_Situácia_Výsledok`, napr. `Create_NegotiableWithAmount_Throws`

## CI

`.github/workflows/ci.yml` spustí pri každom PR build a všetky testy backendu a lint, kontrolu typov a build webu.
