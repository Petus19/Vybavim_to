# Web

The platform's frontend: **Next.js 16** (React, TypeScript), Tailwind CSS 4 and [shadcn/ui](https://ui.shadcn.com) components. The UI is in Slovak (the target market).

No business logic lives here – the web app calls the API through a typed client generated in [`Shared/`](../Shared/README.md).

> **Status:** the web app shows real data from the backend API (`Backend/Api`): categories, search, listing detail, sign-in (email/password and Google), creating, editing and withdrawing listings, "my listings", **offers** – sending, comparing, accepting, rejecting and withdrawing them – and the **AI assistant** (the agent from `Backend/AiService`) for signed-in users.

## Running

The web app needs the API running. The Node version is pinned in `.nvmrc` (Node 24).

```bash
docker compose up -d                       # in the repository root – database
cd Backend && dotnet run --project Api     # API on http://localhost:5080
cd Frontend/Web && npm install && npm run dev
```

The web app runs on http://localhost:3000. If the API is not running, the page shows "Niečo sa nepodarilo" (*something went wrong*) with instructions (`src/app/error.tsx`).

The API address can be changed in `.env.local` (template `.env.example`): `API_URL=http://localhost:5080`.

The **AI assistant** additionally needs the AI service (`Backend/AiService`, see its README) on `AI_SERVICE_URL` (default `http://127.0.0.1:8000`) and a model – the local Ollama or Claude (`AGENT_PROVIDER`). Without it the rest of the web works; the assistant only says it is not available.

| Command | What it does |
| --- | --- |
| `npm run dev` | development server with hot reload |
| `npm run build` | production build (also catches errors in pre-rendered pages) |
| `npm run lint` | ESLint |
| `npm run typecheck` | generates Next.js route types and runs the TypeScript check |
| `npm run format` | formats the code (Prettier + Tailwind class sorting) |
| `npm run api:types` | regenerates the API types from OpenAPI (the API must be running) |

## Talking to the API

- **`src/lib/api.ts`** is the only place that calls the API – an [`openapi-fetch`](https://openapi-ts.dev/openapi-fetch/) client typed by `Frontend/Shared/api/schema.d.ts`, so TypeScript checks URLs, parameters and responses.
- **Only the server calls the API** (Server Components and Server Actions), so no CORS is needed and the API address (`API_URL`) never reaches the browser.
- **Data is fetched per request**, not at `next build` time (`connection()` in `api.ts`), so the build and CI don't need a running API. Components that call the API must be inside `<Suspense>`.
- **Errors:** an invalid filter (400) shows a message in the list, a missing listing (404) shows the "not found" page, an unreachable API shows an error page with a *Try again* button.
- After an API change in the backend: run `npm run api:types` and commit `Frontend/Shared/api/schema.d.ts`.

## Pages

| URL | File | Contents |
| --- | --- | --- |
| `/` | `src/app/page.tsx` | home: search, categories, latest listings, how it works |
| `/inzeraty` | `src/app/inzeraty/page.tsx` | listings with search, categories, a "Filters" panel and sorting |
| `/inzeraty/[id]` | `src/app/inzeraty/[id]/page.tsx` | detail: facts as tiles, deliverables, skills, requirements, similar listings |
| `/prihlasenie` | `src/app/prihlasenie/page.tsx` | sign in and register (tabs, `?rezim=registracia`, `?navrat=` return path) |
| `/inzeraty/[id]/ponuka` | `src/app/inzeraty/[id]/ponuka/` | send an offer (price, delivery days, message) – **signed in only** |
| `/pridat-inzerat` | `src/app/pridat-inzerat/` | create a listing in 3 steps – **signed in only** |
| `/inzeraty/[id]/upravit` | `src/app/inzeraty/[id]/upravit/` | edit a listing – **author only**, open listings only |
| `/moje-inzeraty` | `src/app/moje-inzeraty/` | my listings in all statuses, withdraw – **signed in only** |
| `/moje-ponuky` | `src/app/moje-ponuky/` | offers I sent with their status, withdraw a pending one – **signed in only** |

### Creating and editing a listing

One form for both (`src/components/listing-form.tsx`) in three steps: **what you need** (category, title, description, skills, deliverables, requirements) → **budget and deadline** (price, level, scope, engagement, deadline) → **where** (remote / city, invoice, card preview).

- **Validation** in `src/lib/listing-form.ts` mirrors the backend (`Listing.Normalize`): title max 120 characters, description max 5000, 1–10 skills, amount > 0 unless negotiable, city required unless remote, deadline not in the past. Errors appear after "Continue" and focus jumps to the first invalid field.
- **Saving:** the Server Action `saveListing` (`src/app/pridat-inzerat/actions.ts`) validates again and calls `POST /api/v1/listings` or `PUT /api/v1/listings/{id}`. An API error with a code (e.g. `listing.title_too_long`) is shown next to the matching field (`fieldByErrorCode`), anything else above the button. After saving it redirects to the listing detail.
- **Editing** is available to the author ("Edit" in My listings and on the detail). Someone else's listing shows "not found", a listing that is no longer open shows an explanation.

### My listings

- The list comes from `GET /api/v1/listings/mine` (all statuses, newest first, 20 per page, `?strana=`). The link is in the account menu in the header.
- Only an open listing can be **withdrawn** – the button asks for confirmation inline (`src/components/cancel-listing-button.tsx`), then the Server Action `cancelListing` (`src/app/moje-inzeraty/actions.ts`) calls `POST /api/v1/listings/{id}/cancel` and refreshes the page. If the status changed in the meantime (409), the current status is shown.
- A withdrawn listing is not in search results and others can't see it (404). The **author** still sees it on the detail page because the web app sends their token too (`getListing(id, token)`), and gets "This is your listing" instead of "Send an offer".

### Listing filters (URL parameters)

Filters live in the URL, so a result can be shared as a link. Empty fields are left out of the URL (`src/components/search-form.tsx`). The web app maps its Slovak parameter names to the API parameters (`toApiQuery()` in `src/app/inzeraty/page.tsx`; see `Backend/Api/README.md`).

| Parameter | Values | Meaning |
| --- | --- | --- |
| `q` | text | search in title, description and skills (ignores diacritics) |
| `kategoria` | `web`, `grafika`, `it-podpora`, … | category |
| `typ` | `pevna`, `hodinova`, `dohodou` | price type: fixed, hourly, negotiable |
| `cena_od`, `cena_do` | number (€) | **fixed price** range |
| `sadzba_od`, `sadzba_do` | number (€/hour) | **hourly rate** range |
| `mesto` | city name | location |
| `uroven` | `zaciatocnik`, `skuseny`, `expert` | expected experience level |
| `spolupraca` | `jednorazovo`, `dlhodobo` | one-off or ongoing |
| `na_dialku`, `otvorene` | `1` | remote only / open listings only |
| `bez_faktury` | `1` | listings that don't require an invoice |
| `zoradit` | `najlacnejsie`, `najdrahsie` | sort (default: newest) |
| `strana` | number | page (24 listings per page) |

Fixed prices and hourly rates are **never compared with each other** – ranges and price sorting are handled by the backend (`SearchListings`).

### Offers

| Who | Where | What |
| --- | --- | --- |
| Specialist | "Poslať ponuku" on a listing → `/inzeraty/[id]/ponuka` | price (€, comma or dot), delivery in days, message for the client (max 2,000 characters). Validation in `src/lib/offers.ts` mirrors the backend (`Offer.Create`); API errors with a code (`offer.invalid_price` …) appear next to the field. After sending → `/moje-ponuky`. |
| Specialist | listing detail | instead of the button: "Tvoja ponuka: 280 € · Čaká na rozhodnutie" with a link to My offers |
| Specialist | `/moje-ponuky` | all sent offers, newest first, status badge; **Withdraw** for a pending offer |
| Client (author) | listing detail, section **Ponuky** | every offer with the specialist's name, city, price, delivery, message, and – from their public profile – bio and skills to compare; **Accept** / **Reject** on pending offers of an open listing |

- The page for sending an offer shows the reason instead of the form when the offer can't be sent: your own listing, a listing that no longer accepts offers, or an offer already sent (the API would refuse it anyway).
- **Accept, reject and withdraw** ask for confirmation in place (`src/components/confirm-action-button.tsx`) and say what will happen – accepting moves the listing to "In progress" and **rejects the other pending offers**, which cannot be undone. The Server Action `decideOffer` (`src/app/moje-ponuky/actions.ts`) calls `POST /api/v1/offers/{id}/accept|reject|withdraw`; who may do what is checked by the API.
- Offers come from `getListingOffers` (author only), `getMyOffers` and `getProfile` in `src/lib/api.ts`. "My offers" is also in the account menu.

### AI assistant

A chat panel for signed-in users ("Asistent", bottom right; `src/components/agent/`). It is the same agent as the terminal one in `Backend/AiService` – it searches listings, compares offers and drafts a listing or an offer through the public API, with the user's own permissions.

| Part | Where | What |
| --- | --- | --- |
| Bridge to the AI service | `src/app/api/agent/chat/route.ts`, `…/confirm/route.ts` (Route Handlers) | adds the access token from the httpOnly cookie and forwards the request; the agent's steps come back as **Server-Sent Events**, passed through unchanged. The token never reaches the browser's JavaScript. |
| Events and the stream reader | `src/lib/agent.ts` | event types (`tool_call`, `confirmation_required`, `message` …) and `readAgentEvents` |
| Panel | `src/components/agent/agent-chat.tsx` | role switch (*Zadávam prácu* / *Hľadám prácu* – the agent's role for the conversation), suggested requests, live steps, the answer with model, time and estimated cost; **Stop** cancels the run |
| Confirmation card | `src/components/agent/agent-parts.tsx` | every write (new listing, offer, accept/reject) shows a preview with **Potvrdiť / Zrušiť** and a 5-minute countdown – the agent waits on the server until you decide |
| Visible only signed in | `src/components/agent/agent-launcher.tsx` in `app/layout.tsx` | in the root layout, so the conversation survives navigation (e.g. clicking the link to the created listing) |

- **Nothing is written without a click:** declining, letting the confirmation expire, pressing Stop or closing the tab all leave the data unchanged (the AI service declines a pending confirmation when the stream is closed).
- After a write the current page is refreshed (`router.refresh()`), and the step shows **"Otvoriť na webe →"** – the new listing or offer is visibly real.
- The model's answer is rendered as text (paragraphs, lists, **bold**, links) without a Markdown library and without HTML, so nothing from the model's output is executed.

### Not yet in the web app

- **Listing author card** – the API now returns `owner` (from the Profiles module) on the listing detail; the card and a "my profile" page are the next web step.
- **Number of offers** in the "My listings" list – shown on the listing detail for now (one call per listing would be needed in the list).
- **City list** for the filter – hard-coded in `src/lib/listings.ts` for now.

## Authentication

Anyone can browse listings. Sign-in is required only **for an action** (send an offer, post a listing), and afterwards the user **returns exactly where they were going** (`?navrat=`).

| File | What it does |
| --- | --- |
| `src/lib/auth.ts` | `getCurrentUser()` (via `GET /api/v1/auth/me`), `requireUser(returnPath)` for protected pages, `requireSession(returnPath)` – also returns the token for protected endpoints, `safeReturnPath()` |
| `src/app/prihlasenie/actions.ts` | Server Actions `login`, `register` (signs in right after registration), `logout` |
| `src/components/auth-form.tsx` | form with API errors and live password-rule hints |
| `src/components/account-menu.tsx` | "Sign in" or an avatar with a menu and sign-out in the header |
| `src/proxy.ts` | when the access token (1 h) expires, gets new tokens from the API using the refresh token (14 days) |
| `src/lib/auth-cookies.ts` | cookie names and options |

**Tokens** are stored in the `vybavim_access` and `vybavim_refresh` cookies with the **httpOnly** flag – browser JavaScript can't read them; only the web server calls the API. In production they are also `secure` (HTTPS only).

**Adding a protected page:** a component inside `<Suspense>` calls `await requireUser("/page-path")` – an anonymous user is redirected to sign-in and brought back afterwards (see `src/app/pridat-inzerat/page.tsx`). If the page calls a protected API endpoint, use `requireSession` and pass the token via `authHeaders(token)` (see `src/app/moje-inzeraty/page.tsx`).

**Security:** the return path may only be a path on this site (`/…`); external addresses (`//evil.com`, `https://…`) are ignored, so the sign-in link can't be abused as an open redirect.

**Development account:** in development the API seeds an admin `admin@vybavim.local` (password in `Backend/Api/README.md`). You can register your own account at `/prihlasenie?rezim=registracia`.

### Sign in with Google

The **"Pokračovať s Google"** (*Continue with Google*) button works for both sign-in and registration (OAuth 2.0 with PKCE):

1. `src/app/prihlasenie/google/route.ts` – redirects to Google; stores a random `state`, the PKCE verifier and the return path in a short-lived httpOnly cookie
2. `src/app/prihlasenie/google/callback/route.ts` – verifies `state`, exchanges the code for an ID token (using the client secret), sends it to the API (`POST /api/v1/auth/google`), stores our tokens and returns the user to the return path

The button is shown only when **`.env.local`** contains `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` (template `.env.example`). **Never commit the client secret** – share it with teammates through a password manager, not chat or email.

Google Cloud Console setup (project _Vybavim-to_ → Google Auth Platform):

- **Clients** → Web application, _Authorized redirect URI_ `http://localhost:3000/prihlasenie/google/callback` (the production domain will be added later)
- **Audience** → while the app is in _Testing_ mode, only accounts listed under **Test users** can sign in

Linking to an existing account and creating new accounts is handled by the API – see `Backend/Api/README.md`, "Sign in with Google".

### Not yet implemented (backend)

- **Server-side sign-out** – signing out deletes the cookies, but the refresh token stays valid in the API for up to 14 days (no revocation endpoint yet).
- **Password reset, email verification, Apple sign-in** – will appear once the API supports them.

## Design

We follow the principles of Apple's [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines) (not the Apple brand):

- **One accent colour:** the brand green (`--brand`, `--primary`) for buttons and links. Orange (`--brand-orange`) only in the logo.
- **Neutral surfaces:** white background, sections and cards separated by a grey surface `bg-surface` (`#f5f5f7`), no borders or shadows.
- **Lots of space, less information:** a listing card shows only category, title, city and budget; everything else is on the detail page.
- **Bold headings:** `font-semibold tracking-tight`, large sizes (`text-5xl` to `text-7xl` on the home page).
- **Pill-shaped buttons and inputs** (`rounded-full`), cards `rounded-3xl`.
- **Text links with a chevron** (`<ChevronLink>`).
- **Font:** Inter with the `latin-ext` subset (Slovak diacritics), the closest free equivalent of SF Pro.

Also:

- **Logo** (`src/components/logo.tsx`): orange dot = client, green dot = specialist, line = a "done" tick. Favicon: `src/app/icon.svg`.
- **Colours** are design tokens in `src/app/globals.css` for light and dark mode – a design change happens only there.
- Texts with counts use `plural()` from `src/lib/listings.ts` (Slovak plural forms).

## Light and dark mode

- A ☀︎/☾ icon in the header toggles quickly; the footer offers **Auto / Light / Dark** (`src/components/theme-toggle.tsx`).
- The choice is stored in `localStorage` (`theme`); "Auto" removes it and follows the system.
- `<html>` gets `data-theme="light" | "dark"`. A script in `<head>` (`src/lib/theme.ts`) sets it before the first paint, so the page doesn't flash.
- Dark colours are in `globals.css` under `:root[data-theme="dark"]`; Tailwind's `dark:` variant follows the same attribute.

## Next.js 16 notes

This version has **Cache Components** enabled (`next.config.ts`). `params` and `searchParams` are `Promise`s, and a component that reads them (or calls the API) must be inside `<Suspense>`, otherwise the dev server warns that navigation won't be instant. See `src/app/inzeraty/page.tsx`. Up-to-date docs are in `node_modules/next/dist/docs/`.

If a change in `src/app/globals.css` (e.g. new colours) doesn't show up, stop the dev server, clear the cache and start again:

```bash
rm -rf .next && npm run dev
```
