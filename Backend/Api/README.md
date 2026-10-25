# Api

The backend host: REST API (`/api/v1/…`) with OpenAPI documentation.

It contains no business logic – it only wires up shared infrastructure and the modules from `Modules/`:

```csharp
builder.Services.AddIdentityModule(builder.Configuration);  // Modules/Identity
builder.Services.AddProfilesModule(builder.Configuration);  // Modules/Profiles
builder.Services.AddListingsModule(builder.Configuration);  // Modules/Listings

app.MapIdentityEndpoints();
app.MapProfilesEndpoints();
app.MapListingsEndpoints();
```

## Running

```bash
docker compose up -d          # in the repository root – database and other services
cd Backend
dotnet run --project Api
```

The API runs on http://localhost:5080. In development it applies database migrations on start-up and loads demo data into empty tables.

| URL | What is there |
|---|---|
| http://localhost:5080/scalar | interactive API documentation |
| http://localhost:5080/openapi/v1.json | OpenAPI document – the web client generates its TypeScript types from it |
| http://localhost:5080/api/v1/health | health check |

## Authentication

Users, passwords and roles are managed by **ASP.NET Core Identity** (module `Identity`, schema `identity`).

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/v1/auth/register` | public | `{ "email", "password" }` → 201 + user |
| POST | `/api/v1/auth/login` | public | `{ "login", "password" }` – login is an email or a user name → tokens |
| POST | `/api/v1/auth/google` | public | `{ "idToken" }` – Google ID token → the same tokens as a password sign-in |
| POST | `/api/v1/auth/refresh` | public | `{ "refreshToken" }` → new tokens |
| GET | `/api/v1/auth/me` | signed in | current user: `id`, `userName`, `email`, `roles` |
| GET | `/api/v1/users` | **Admin** only | all users with their roles |

Sign-in response:

```json
{ "tokenType": "Bearer", "accessToken": "…", "expiresIn": 3600, "refreshToken": "…" }
```

- The **access token** is sent as `Authorization: Bearer <accessToken>` and is valid for 1 hour.
- The **refresh token** is valid for 14 days and is only used to obtain a new access token (`/auth/refresh`). Changing the password invalidates existing refresh tokens (security stamp).
- **Password policy:** at least 8 characters, upper- and lower-case letter, digit. The account is locked for 15 minutes after 5 failed attempts.
- Errors: `identity.registration_failed` (400, message in Slovak), `identity.invalid_credentials`, `identity.locked_out`, `identity.invalid_refresh_token` (401). No token → 401, insufficient role → 403.

### Sign in with Google

The web app obtains a Google **ID token** (OAuth 2.0 with PKCE, `Frontend/Web/src/app/prihlasenie/google/`) and posts it to `POST /api/v1/auth/google`. The API (`LoginWithGoogle`):

1. validates the token signature with Google and checks it was issued for **our Client ID** (`Authentication:Google:ClientId` in `appsettings.Development.json` – not a secret),
2. rejects Google accounts with an unverified email,
3. finds the user by their Google login link (`identity.user_logins`); on first sign-in it links Google to an existing account with the same email, otherwise it creates a **new account without a password**,
4. returns the same tokens as a password sign-in.

Errors: `identity.invalid_google_token`, `identity.google_email_not_verified` (401), `identity.google_not_configured` (400 – Client ID missing). The API does not need the **client secret**; only the web app has it (`Frontend/Web/.env.local`, never committed).

### Development admin account

| User name | Email | Password | Role |
|---|---|---|---|
| `Admin` | `admin@vybavim.local` | `Admin123` | Admin |

Created by `IdentityDevSeeder` in the Development environment only – **it never exists in production**.

**In Scalar:** call `POST /api/v1/auth/login`, copy `accessToken`, click **Authentication** (Bearer) at the top and paste it. Protected endpoints then work too.

## Profiles

Public user profile (module `Profiles`, schema `profiles`). Each user has at most one profile; `GET /profiles/me` returns 404 until it is created.

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/v1/profiles/me` | signed in | my profile; 404 `profile.not_found` until created |
| PUT | `/api/v1/profiles/me` | signed in | create or update my profile |
| GET | `/api/v1/profiles/{userId}` | public | public profile |

```json
{
  "type": "person",
  "displayName": "Jana Kováčová",
  "city": "Košice",
  "bio": "Graphic designer with 5 years of experience.",
  "skills": ["Branding", "Illustrator"],
  "website": "https://jana-grafika.sk"
}
```

- `type`: `person` or `company`; `type` and `displayName` are required, the rest is optional
- rules: name max 100 characters, city 100, bio 2000, max 20 skills of 50 characters, website must be a full `http(s)://` URL (no `javascript:` links)
- errors: `profile.display_name_required`, `profile.display_name_too_long`, `profile.city_too_long`, `profile.bio_too_long`, `profile.too_many_skills`, `profile.skill_too_long`, `profile.invalid_website` (400), `profile.not_found` (404)
- other modules read profiles only through the contract `IProfilesApi` in `Profiles.Contracts` (used by Listings to show the listing author)
- in development all authors of the demo listings have a profile (`ProfilesDevSeeder`)

## Listings

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/categories` | main categories (`slug`, `name`, `description`) |
| GET | `/api/v1/listings` | search with filters, sorting and paging |
| GET | `/api/v1/listings/{id}` | listing detail; 404 if it does not exist (a withdrawn listing is visible only to its author and admins) |
| POST | `/api/v1/listings` | **signed in** – create a listing; the signed-in user becomes the author → 201 + detail |
| PUT | `/api/v1/listings/{id}` | **author only** – update, only while the listing is open → 200 + detail |
| POST | `/api/v1/listings/{id}/cancel` | **author or admin** – withdraw (status `cancelled`) → 200 + detail |
| GET | `/api/v1/listings/mine` | **signed in** – my listings in all statuses, newest first (`page`, `pageSize`) |

Withdrawn listings never appear in public search.

### Creating and updating a listing

The request body has the same shape as the detail response, so the web app uses one form for both:

```json
{
  "category": "grafika",
  "title": "Logo for a café",
  "description": "We are looking for a designer…",
  "budget": { "type": "fixed", "amount": 300 },
  "city": "Bratislava",
  "remote": true,
  "deadline": "2026-11-15",
  "level": "intermediate",
  "scope": { "size": "medium", "duration": "2–4 weeks" },
  "engagement": "one_time",
  "invoiceRequired": true,
  "skills": ["Branding", "Adobe Illustrator"],
  "deliverables": ["Vector logo"],
  "requirements": []
}
```

- all fields are required except `city` (when `remote: true`), `deadline`, `deliverables` and `requirements`; a missing required field → 400
- rules (enforced in the domain entity `Listing`): title max 120 characters, description max 5000, `negotiable` budget has no amount, otherwise amount > 0, city required unless remote, deadline not in the past, 1–10 skills, lists max 10 items of 100 characters

### Search filters – `GET /api/v1/listings`

All parameters are optional. The web app uses Slovak names in its own URLs and maps them to these.

| API parameter | Web parameter | Values |
|---|---|---|
| `q` | `q` | text; searches title, description and skills, ignoring case and diacritics |
| `category` | `kategoria` | `web`, `grafika`, `it-podpora`, `marketing` |
| `city` | `mesto` | city name |
| `budgetType` | `typ` | `fixed`, `hourly`, `negotiable` |
| `priceMin`, `priceMax` | `cena_od`, `cena_do` | € – fixed prices only |
| `rateMin`, `rateMax` | `sadzba_od`, `sadzba_do` | €/hour – hourly rates only |
| `level` | `uroven` | `beginner`, `intermediate`, `expert` |
| `engagement` | `spolupraca` | `one_time`, `ongoing` |
| `remote` | `na_dialku` | `true` = remote only |
| `openOnly` | `otvorene` | `true` = open listings only |
| `noInvoice` | `bez_faktury` | `true` = no invoice required |
| `sort` | `zoradit` | `newest` (default), `cheapest`, `priciest` |
| `page`, `pageSize` | `strana` | paging; `pageSize` 1–50, default 20 |

Fixed prices and hourly rates are **never compared with each other** – each range applies only to its own price type, and sorting by price puts fixed prices first, then hourly rates, then negotiable.

Response: `{ "items": [...], "total": 12, "page": 1, "pageSize": 20 }`.

### Data format

- Enums are `snake_case` strings: `"in_progress"`, `"one_time"`, `"negotiable"`.
- Budget: `{ "type": "fixed", "amount": 300.00 }`; negotiable: `{ "type": "negotiable", "amount": null }`.
- The listing detail includes its author from the Profiles module: `"owner": { "id", "displayName", "type", "city" }` – `null` when the author has no profile yet (`ownerId` is always present).

### Errors

Errors are RFC 9457 ProblemDetails with a `code` field for the client. The human-readable `title` is in Slovak (the product's language):

```json
{ "status": 404, "title": "Inzerát neexistuje.", "code": "listing.not_found" }
```

| Status | When | `code` |
|---|---|---|
| 400 | invalid filter, price range or page | `listing.invalid_filter`, `listing.invalid_sort`, `listing.invalid_range`, `listing.invalid_page` |
| 400 | a listing rule is broken | `listing.title_required`, `listing.title_too_long`, `listing.description_required`, `listing.description_too_long`, `listing.duration_required`, `listing.duration_too_long`, `listing.budget_amount_not_allowed`, `listing.budget_amount_required`, `listing.city_required`, `listing.city_too_long`, `listing.deadline_in_past`, `listing.skills_required`, `listing.too_many_items`, `listing.item_too_long`, `listing.unknown_category` |
| 403 | someone else's listing | `listing.not_owner`, `listing.cancel_not_allowed` |
| 404 | listing does not exist | `listing.not_found` |
| 409 | listing is no longer open (update, withdraw) | `listing.not_editable`, `listing.invalid_status_change` |

## CORS

The web app runs on a different origin, so the browser must be allowed to call the API. Allowed origins are configured in `appsettings.Development.json` under `Cors:AllowedOrigins` (`http://localhost:3000` in development).
