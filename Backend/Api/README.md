# Api

Spúšťací projekt backendu: REST API (`/api/v1/…`, OpenAPI) + WebSockety (SignalR).

Neobsahuje biznis logiku, iba zapojí spoločné nastavenia a jednotlivé moduly z `Modules/`:

```csharp
builder.Services.AddIdentityModule(builder.Configuration);  // Modules/Identity
builder.Services.AddListingsModule(builder.Configuration);  // Modules/Listings

app.MapIdentityEndpoints();
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

## Prihlásenie

Používateľov, heslá a role spravuje **ASP.NET Core Identity** (modul `Identity`, schéma `identity`).

| Metóda | Adresa | Kto | Popis |
|---|---|---|---|
| POST | `/api/v1/auth/register` | ktokoľvek | `{ "email", "password" }` → 201 + údaje o používateľovi |
| POST | `/api/v1/auth/login` | ktokoľvek | `{ "login", "password" }` – login je e-mail alebo meno → tokeny |
| POST | `/api/v1/auth/google` | ktokoľvek | `{ "idToken" }` – ID token od Google → rovnaké tokeny ako pri hesle |
| POST | `/api/v1/auth/refresh` | ktokoľvek | `{ "refreshToken" }` → nové tokeny |
| GET | `/api/v1/auth/me` | prihlásený | kto som: `id`, `userName`, `email`, `roles` |
| GET | `/api/v1/users` | iba **Admin** | zoznam všetkých používateľov s rolami |

Odpoveď prihlásenia:

```json
{ "tokenType": "Bearer", "accessToken": "…", "expiresIn": 3600, "refreshToken": "…" }
```

- **accessToken** sa posiela v hlavičke `Authorization: Bearer <accessToken>`, platí 1 hodinu.
- **refreshToken** platí 14 dní a slúži iba na získanie nového accessTokenu (`/auth/refresh`).
- **Heslo:** aspoň 8 znakov, veľké a malé písmeno, číslica. Po 5 zlých heslách sa účet na 15 minút zablokuje.
- Chyby: `identity.registration_failed` (400, hláška po slovensky), `identity.invalid_credentials`, `identity.locked_out`, `identity.invalid_refresh_token` (401). Bez tokenu 401, bez oprávnenia 403.

### Prihlásenie cez Google

Web si od Google vypýta **ID token** (OAuth s PKCE, `Frontend/Web/src/app/prihlasenie/google/`) a pošle ho na `POST /api/v1/auth/google`. API (`LoginWithGoogle`):

1. overí podpis tokenu u Google a že bol vydaný pre **náš Client ID** (`Authentication:Google:ClientId` v `appsettings.Development.json`, nie je tajný),
2. odmietne Google účet s neovereným e-mailom,
3. nájde používateľa podľa prepojenia s Google (`identity.user_logins`); pri prvom prihlásení pripojí Google k existujúcemu účtu s rovnakým e-mailom, inak vytvorí **nový účet bez hesla**,
4. vráti rovnaké tokeny ako pri prihlásení heslom.

Chyby: `identity.invalid_google_token`, `identity.google_email_not_verified` (401), `identity.google_not_configured` (400 – chýba Client ID). **Client secret** API nepotrebuje, ten má iba web (`Frontend/Web/.env.local`).

### Testovací admin (iba vo vývoji)

| Meno | E-mail | Heslo | Rola |
|---|---|---|---|
| `Admin` | `admin@vybavim.local` | `Admin123` | Admin |

Vytvára ho `IdentityDevSeeder` iba v prostredí Development – **na produkčnom serveri nikdy nevznikne**.

**V Scalar:** zavolaj `POST /api/v1/auth/login`, skopíruj `accessToken`, hore klikni na **Authentication** (Bearer) a vlož ho. Potom fungujú aj chránené endpointy.

## Profily

Verejný profil používateľa (modul `Profiles`, schéma `profiles`). Každý používateľ má najviac jeden profil; kým ho nevyplní, `GET /profiles/me` vráti 404.

| Metóda | Adresa | Kto | Popis |
|---|---|---|---|
| GET | `/api/v1/profiles/me` | prihlásený | môj profil, 404 `profile.not_found` kým ho nevyplním |
| PUT | `/api/v1/profiles/me` | prihlásený | uloží môj profil – prvýkrát ho vytvorí, potom upraví |
| GET | `/api/v1/profiles/{userId}` | ktokoľvek | verejný profil |

```json
{
  "type": "person",
  "displayName": "Jana Kováčová",
  "city": "Košice",
  "bio": "Grafička s 5-ročnou praxou.",
  "skills": ["Branding", "Illustrator"],
  "website": "https://jana-grafika.sk"
}
```

- `type`: `person` alebo `company`; povinné sú `type` a `displayName`, ostatné nepovinné
- pravidlá: meno max. 100 znakov, mesto 100, popis 2000, max. 20 zručností po 50 znakov, web iba celá adresa `http(s)://`
- chyby: `profile.display_name_required`, `profile.display_name_too_long`, `profile.city_too_long`, `profile.bio_too_long`, `profile.too_many_skills`, `profile.skill_too_long`, `profile.invalid_website` (400), `profile.not_found` (404)
- vo vývoji majú profily všetci zadávatelia testovacích inzerátov (`ProfilesDevSeeder`)

## Endpointy inzerátov

| Metóda | Adresa | Popis |
|---|---|---|
| GET | `/api/v1/categories` | hlavné kategórie (`slug`, `name`, `description`) |
| GET | `/api/v1/listings` | vyhľadávanie inzerátov s filtrami, zoradením a stránkovaním |
| GET | `/api/v1/listings/{id}` | detail inzerátu, 404 ak neexistuje (stiahnutý vidí iba autor a admin) |
| POST | `/api/v1/listings` | **prihlásený** – pridá inzerát, autorom je prihlásený používateľ → 201 + detail |
| PUT | `/api/v1/listings/{id}` | **iba autor** – úprava, iba kým je inzerát otvorený → 200 + detail |
| POST | `/api/v1/listings/{id}/cancel` | **autor alebo admin** – stiahnutie (stav `cancelled`) → 200 + detail |
| GET | `/api/v1/listings/mine` | **prihlásený** – moje inzeráty vo všetkých stavoch, najnovšie prvé (`page`, `pageSize`) |

Stiahnuté inzeráty sa vo verejnom vyhľadávaní nezobrazujú.

### Pridanie a úprava inzerátu

Telo požiadavky má rovnaký tvar ako detail, takže web môže použiť jeden formulár na pridanie aj úpravu:

```json
{
  "category": "grafika",
  "title": "Logo pre kaviareň",
  "description": "Hľadáme grafika na logo…",
  "budget": { "type": "fixed", "amount": 300 },
  "city": "Bratislava",
  "remote": true,
  "deadline": "2026-11-15",
  "level": "intermediate",
  "scope": { "size": "medium", "duration": "2–4 týždne" },
  "engagement": "one_time",
  "invoiceRequired": true,
  "skills": ["Branding", "Adobe Illustrator"],
  "deliverables": ["Logo vo vektoroch"],
  "requirements": []
}
```

- povinné sú všetky polia okrem `city` (ak `remote: true`), `deadline`, `deliverables` a `requirements`; chýbajúce povinné pole → 400
- pravidlá: názov max. 120 znakov, popis max. 5000, pri `negotiable` bez sumy, inak suma > 0, bez práce na diaľku je mesto povinné, termín nie v minulosti, 1–10 zručností, zoznamy max. 10 položiek po 100 znakov

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
- Detail inzerátu má autora z modulu Profiles: `"owner": { "id", "displayName", "type", "city" }`. Ak autor ešte nemá profil, je `owner: null` (`ownerId` je vždy). Hodnotenie a počet projektov prídu s modulmi Reviews a Projects.

### Chyby

Chyby majú formát ProblemDetails (RFC 9457) a pole `code` pre frontend:

```json
{ "status": 404, "title": "Inzerát neexistuje.", "code": "listing.not_found" }
```

| Stav | Kedy | `code` |
|---|---|---|
| 400 | neplatný filter, rozsah ceny alebo stránka | `listing.invalid_filter`, `listing.invalid_sort`, `listing.invalid_range`, `listing.invalid_page` |
| 400 | porušené pravidlo inzerátu (hláška po slovensky v `title`) | `listing.title_required`, `listing.title_too_long`, `listing.description_required`, `listing.description_too_long`, `listing.duration_required`, `listing.duration_too_long`, `listing.budget_amount_not_allowed`, `listing.budget_amount_required`, `listing.city_required`, `listing.city_too_long`, `listing.deadline_in_past`, `listing.skills_required`, `listing.too_many_items`, `listing.item_too_long`, `listing.unknown_category` |
| 403 | cudzí inzerát | `listing.not_owner`, `listing.cancel_not_allowed` |
| 404 | inzerát neexistuje | `listing.not_found` |
| 409 | inzerát nie je otvorený (úprava, stiahnutie) | `listing.not_editable`, `listing.invalid_status_change` |

## CORS

Web beží na inej adrese ako API, preto ho prehliadač musí mať povolený. Povolené adresy sú v `appsettings.Development.json` v `Cors:AllowedOrigins` (vo vývoji `http://localhost:3000`).
