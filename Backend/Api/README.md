# Api

Spúšťací projekt backendu: REST API (`/api/v1/…`, OpenAPI) + WebSockety (SignalR).

Neobsahuje biznis logiku, iba zapojí spoločné nastavenia a jednotlivé moduly z `Modules/`:

```csharp
builder.Services.AddListingsModule(builder.Configuration);  // Modules/Listings

app.MapListingsEndpoints();
```

## Spustenie

```bash
docker compose up -d          # v koreni repa – databáza a ostatné služby
cd Backend
dotnet run --project Api
```

API beží na http://localhost:5080. Vo vývoji sa pri štarte aplikujú migrácie a do prázdnych tabuliek sa vložia testovacie dáta.

| Adresa | Čo tam je |
|---|---|
| http://localhost:5080/scalar | dokumentácia API, dá sa v nej API priamo skúšať |
| http://localhost:5080/openapi/v1.json | popis API vo formáte OpenAPI – z neho si web vygeneruje typy |
| http://localhost:5080/api/v1/health | kontrola, či API beží |

## Endpointy

| Metóda | Adresa | Popis |
|---|---|---|
| GET | `/api/v1/categories` | hlavné kategórie (`slug`, `name`, `description`) |
| GET | `/api/v1/listings` | vyhľadávanie inzerátov s filtrami, zoradením a stránkovaním |
| GET | `/api/v1/listings/{id}` | detail inzerátu, 404 ak neexistuje |

### Filtre `GET /api/v1/listings`

Všetky parametre sú nepovinné. Pravidlá sú rovnaké ako na webe (`Frontend/Web/src/lib/listings.ts`).

| Parameter API | Parameter webu | Hodnoty |
|---|---|---|
| `q` | `q` | text; hľadá v názve, popise a zručnostiach, bez ohľadu na diakritiku |
| `category` | `kategoria` | `web`, `grafika`, `it-podpora`, `marketing` |
| `city` | `mesto` | názov mesta |
| `budgetType` | `typ` | `fixed`, `hourly`, `negotiable` |
| `priceMin`, `priceMax` | `cena_od`, `cena_do` | € – iba pevná cena |
| `rateMin`, `rateMax` | `sadzba_od`, `sadzba_do` | €/hod. – iba hodinová sadzba |
| `level` | `uroven` | `beginner`, `intermediate`, `expert` |
| `engagement` | `spolupraca` | `one_time`, `ongoing` |
| `remote` | `na_dialku` | `true` = iba na diaľku |
| `openOnly` | `otvorene` | `true` = iba otvorené |
| `noInvoice` | `bez_faktury` | `true` = iba bez faktúry |
| `sort` | `zoradit` | `newest` (predvolené), `cheapest`, `priciest` |
| `page`, `pageSize` | – | stránkovanie; `pageSize` 1–50, predvolene 20 |

Odpoveď: `{ "items": [...], "total": 12, "page": 1, "pageSize": 20 }`.

### Formát dát

- Enumy sú text v tvare snake_case: `"in_progress"`, `"one_time"`, `"negotiable"`.
- Rozpočet: `{ "type": "fixed", "amount": 300.00 }`, pri cene dohodou `{ "type": "negotiable", "amount": null }`.
- Údaje o zadávateľovi zatiaľ nie sú, inzerát má iba `ownerId`. Meno, hodnotenie a ďalšie prídu s modulmi Identity, Profiles a Reviews.

### Chyby

Chyby majú formát ProblemDetails (RFC 9457) a pole `code` pre frontend:

```json
{ "status": 404, "title": "Inzerát neexistuje.", "code": "listing.not_found" }
```

| Stav | Kedy | `code` |
|---|---|---|
| 400 | neplatný filter, rozsah ceny alebo stránka | `listing.invalid_filter`, `listing.invalid_sort`, `listing.invalid_range`, `listing.invalid_page` |
| 404 | inzerát neexistuje | `listing.not_found` |

## CORS

Web beží na inej adrese ako API, preto ho prehliadač musí mať povolený. Povolené adresy sú v `appsettings.Development.json` v `Cors:AllowedOrigins` (vo vývoji `http://localhost:3000`).
