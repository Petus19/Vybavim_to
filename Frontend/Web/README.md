# Web

Frontend platformy: **Next.js 16** (React, TypeScript), Tailwind CSS 4 a komponenty [shadcn/ui](https://ui.shadcn.com).

Biznis logika sem nepatrí. Web volá API cez typovaného klienta z [`Shared/`](../Shared/README.md).

> **Stav:** web zobrazuje skutočné dáta z backendového API (`Backend/Api`). Kategórie a inzeráty sa načítavajú z `GET /api/v1/categories`, `/listings` a `/listings/{id}`.

## Spustenie

Web potrebuje bežiace API. Verzia Node je v `.nvmrc` (Node 24), s `fnm` sa prepne automaticky.

```bash
docker compose up -d                 # v koreni repozitára – databáza
cd Backend && dotnet run --project Api   # API na http://localhost:5080
cd Frontend/Web && npm install && npm run dev
```

Web beží na http://localhost:3000. Ak API nebeží, stránka ukáže „Niečo sa nepodarilo“ s návodom (`src/app/error.tsx`).

Adresa API sa dá zmeniť v `.env.local` (vzor `.env.example`): `API_URL=http://localhost:5080`.

| Príkaz              | Čo robí                                                      |
| ------------------- | ------------------------------------------------------------ |
| `npm run dev`       | vývojový server s hot reloadom                               |
| `npm run build`     | produkčný build (odhalí aj chyby pri predgenerovaní stránok) |
| `npm run lint`      | ESLint                                                       |
| `npm run typecheck` | kontrola typov                                               |
| `npm run format`    | naformátuje kód (Prettier + zoradenie Tailwind tried)        |
| `npm run api:types` | pregeneruje typy API z OpenAPI (API musí bežať)              |

## Napojenie na API

- **`src/lib/api.ts`** – jediné miesto, kde web volá API. Klient [`openapi-fetch`](https://openapi-ts.dev/openapi-fetch/) s typmi z `Frontend/Shared/api/schema.d.ts`, takže TypeScript skontroluje adresy, parametre aj odpovede.
- **Volá sa iba zo servera** (Server Components), preto nie je potrebný CORS a adresa API (`API_URL`) nie je v prehliadači.
- **Dáta sa načítavajú pri požiadavke**, nie pri `next build` (`connection()` v `api.ts`). Build aj CI preto nepotrebujú bežiace API. Komponenty, ktoré volajú API, musia byť v `<Suspense>`.
- **Chyby:** neplatný filter (400) ukáže správu v zozname, neexistujúci inzerát (404) stránku „nenašli sme“, nedostupné API chybovú stránku s tlačidlom _Skúsiť znova_.
- Po zmene API v backende: `npm run api:types` a commitnúť `Frontend/Shared/api/schema.d.ts`.

## Stránky

| URL                     | Súbor                            | Obsah                                                                          |
| ----------------------- | -------------------------------- | ------------------------------------------------------------------------------ |
| `/`                     | `src/app/page.tsx`               | úvod: vyhľadávanie, kategórie, najnovšie inzeráty, ako to funguje              |
| `/inzeraty`             | `src/app/inzeraty/page.tsx`      | zoznam s vyhľadávaním, kategóriami, panelom „Filtre“ a zoradením               |
| `/inzeraty/[id]`        | `src/app/inzeraty/[id]/page.tsx` | detail: fakty v dlaždiciach, výsledky, zručnosti, požiadavky, podobné inzeráty |
| `/prihlasenie`          | `src/app/prihlasenie/page.tsx`   | prihlásenie a registrácia (záložky, `?rezim=registracia`, `?navrat=`)          |
| `/inzeraty/[id]/ponuka` | `src/app/inzeraty/[id]/ponuka/`  | poslanie ponuky – **iba prihlásený**, zatiaľ „pripravujeme“                    |
| `/pridat-inzerat`       | `src/app/pridat-inzerat/`        | pridanie inzerátu – **iba prihlásený**, zatiaľ „pripravujeme“                  |

### Filtre inzerátov (parametre v URL)

Filtre sú v URL, takže sa výsledok dá zdieľať odkazom. Prázdne polia sa do URL nepridávajú (`src/components/search-form.tsx`). Web ich prekladá na parametre API (`toApiQuery()` v `src/app/inzeraty/page.tsx`, prehľad je v `Backend/Api/README.md`).

| Parameter                | Hodnoty                            | Význam                                                   |
| ------------------------ | ---------------------------------- | -------------------------------------------------------- |
| `q`                      | text                               | hľadanie v názve, popise a zručnostiach (bez diakritiky) |
| `kategoria`              | `web`, `grafika`, `it-podpora`, …  | kategória                                                |
| `typ`                    | `pevna`, `hodinova`, `dohodou`     | typ ceny                                                 |
| `cena_od`, `cena_do`     | číslo (€)                          | rozsah **pevnej ceny**                                   |
| `sadzba_od`, `sadzba_do` | číslo (€/hod.)                     | rozsah **hodinovej sadzby**                              |
| `mesto`                  | názov mesta                        | lokalita                                                 |
| `uroven`                 | `zaciatocnik`, `skuseny`, `expert` | očakávaná úroveň odborníka                               |
| `spolupraca`             | `jednorazovo`, `dlhodobo`          | typ spolupráce                                           |
| `na_dialku`, `otvorene`  | `1`                                | iba práca na diaľku / iba otvorené inzeráty              |
| `bez_faktury`            | `1`                                | iba inzeráty, ktoré nevyžadujú faktúru                   |
| `zoradit`                | `najlacnejsie`, `najdrahsie`       | zoradenie (predvolené: najnovšie)                        |
| `strana`                 | číslo                              | stránkovanie (24 inzerátov na stranu)                    |

Pevná cena a hodinová sadzba sa **nikdy navzájom neporovnávajú** – rozsahy aj zoradenie podľa ceny rieši backend (`SearchListings`).

### Čo z API ešte chýba

- **Zadávateľ** (meno, hodnotenie, overenie, počet projektov) – API vracia iba `ownerId`. Karta zadávateľa na detaile je preto zatiaľ skrytá, pribudne s modulmi Identity, Profiles a Reviews.
- **Počet ponúk** na inzerát – pribudne s odpoveďami na inzeráty (modul Projects).
- **Zoznam miest** pre filter – je zatiaľ napevno v `src/lib/listings.ts`.
- Prílohy, otázky pre uchádzačov a polia podľa kategórie (JSONB) – ďalšia verzia.

## Prihlásenie

Prezerať inzeráty môže každý. Prihlásenie sa vyžaduje až **pri akcii** (poslať ponuku, pridať inzerát, neskôr chat a obľúbené) a po ňom sa človek **vráti presne tam, kam smeroval** (`?navrat=`).

| Súbor                             | Čo robí                                                                                                                           |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `src/lib/auth.ts`                 | `getCurrentUser()` (kto je prihlásený, cez `GET /api/v1/auth/me`), `requireUser(navrat)` pre chránené stránky, `safeReturnPath()` |
| `src/app/prihlasenie/actions.ts`  | Server Actions `login`, `register` (po registrácii rovno prihlási), `logout`                                                      |
| `src/components/auth-form.tsx`    | formulár s chybami z API a priebežnou kontrolou pravidiel hesla                                                                   |
| `src/components/account-menu.tsx` | v hlavičke „Prihlásiť“ alebo avatar s menu a odhlásením                                                                           |
| `src/proxy.ts`                    | keď vyprší access token (1 h), vypýta si od API nový pomocou refresh tokenu (14 dní)                                              |
| `src/lib/auth-cookies.ts`         | názvy a nastavenia cookies                                                                                                        |

**Tokeny** sú v cookies `vybavim_access` a `vybavim_refresh` s príznakom **httpOnly** – JavaScript v prehliadači sa k nim nedostane, API volá iba server webu. V produkcii majú aj `secure` (iba HTTPS).

**Nová chránená stránka:** komponent v `<Suspense>` zavolá `await requireUser("/adresa-stranky")` – neprihláseného presmeruje na prihlásenie a po ňom ho vráti späť. Vzor je v `src/app/pridat-inzerat/page.tsx`.

**Bezpečnosť:** `navrat` smie byť iba adresa tohto webu (`/…`), cudzie adresy (`//evil.com`, `https://…`) sa ignorujú – odkaz na prihlásenie sa tak nedá zneužiť na presmerovanie na podvodnú stránku.

**Testovací účet:** vo vývoji API vytvorí admina `admin@vybavim.local` (heslo v `Backend/Api/README.md`). Vlastný účet si vytvoríš na `/prihlasenie?rezim=registracia`.

### Prihlásenie cez Google

Tlačidlo **„Pokračovať s Google“** funguje pre prihlásenie aj registráciu (OAuth 2.0 s PKCE):

1. `src/app/prihlasenie/google/route.ts` – presmeruje na Google; do krátkodobej httpOnly cookie uloží náhodný `state`, PKCE verifier a `navrat`
2. `src/app/prihlasenie/google/callback/route.ts` – overí `state`, vymení kód za ID token (s Client secret), pošle ho do API (`POST /api/v1/auth/google`), uloží naše tokeny a vráti používateľa na `navrat`

Tlačidlo sa zobrazí iba vtedy, keď sú v **`.env.local`** vyplnené `GOOGLE_CLIENT_ID` a `GOOGLE_CLIENT_SECRET` (vzor `.env.example`). **Client secret nikdy nedávaj do gitu** – kolegom ho pošli bezpečne (napr. cez správcu hesiel), nie cez chat alebo e-mail.

Nastavenie v [Google Cloud Console](https://console.cloud.google.com) (projekt _Vybavim-to_ → Google Auth Platform):

- **Clients** → Web application, _Authorized redirect URI_ `http://localhost:3000/prihlasenie/google/callback` (pre produkciu pribudne adresa produkčnej domény)
- **Audience** → kým je aplikácia v režime _Testing_, prihlásiť sa môžu iba ľudia v zozname **Test users** – nového kolegu treba pridať tam

Prepojenie s existujúcim účtom a vytváranie nových účtov rieši API – pozri `Backend/Api/README.md`, časť „Prihlásenie cez Google“.

### Čo z prihlásenia ešte chýba (backend)

- **Odhlásenie na serveri** – web pri odhlásení zmaže cookies, ale refresh token v API zostáva platný až 14 dní (API nemá endpoint na jeho zneplatnenie).
- **Zabudnuté heslo, overenie e-mailu, Apple** – tlačidlá pribudnú, keď ich bude vedieť API.
- **Meno a rola** (zadávateľ / odborník) – patria do modulu Profiles, registrácia má zatiaľ iba e-mail a heslo.

## Dizajn

Riadime sa princípmi [Apple Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines) (nie značkou Apple):

- **Jedna akcentová farba:** zelená značky (`--brand`, `--primary`) na tlačidlá a odkazy. Oranžová (`--brand-orange`) je iba v logu.
- **Neutrálne plochy:** biele pozadie, sekcie a karty odlíšené sivou plochou `bg-surface` (`#f5f5f7`), bez rámčekov a tieňov.
- **Veľa priestoru, menej informácií:** karta inzerátu ukazuje iba kategóriu, názov, mesto a rozpočet, ostatné je až v detaile.
- **Výrazné nadpisy:** `font-semibold tracking-tight`, veľké veľkosti (`text-5xl` až `text-7xl` na úvode).
- **Tlačidlá a polia v tvare piluliek** (`rounded-full`), karty `rounded-3xl`.
- **Textové odkazy so šípkou** (`<ChevronLink>`): „Všetky inzeráty ›“.
- **Písmo** Inter s podmnožinou `latin-ext` (slovenská diakritika), najbližší voľný ekvivalent písma SF Pro.

Ďalej:

- **Logo** (`src/components/logo.tsx`): oranžová bodka = zadávateľ, zelená = odborník, čiara = fajka „vybavené“. Favicon je `src/app/icon.svg`.
- **Farby** sú design tokeny v `src/app/globals.css` pre svetlý aj tmavý režim. Pri zmene dizajnu sa menia len tam.
- Texty s počtom používajú `plural()` z `src/lib/listings.ts` (1 inzerát, 2 inzeráty, 5 inzerátov).

## Svetlý a tmavý režim

- V hlavičke je ikona ☀︎/☾ na rýchle prepnutie, v päte voľba **Automaticky / Svetlý / Tmavý** (`src/components/theme-toggle.tsx`).
- Voľba sa ukladá v `localStorage` (`theme`). „Automaticky“ ju zmaže a režim sa riadi systémom.
- Na `<html>` sa nastaví `data-theme="light" | "dark"`. Skript v `<head>` (`src/lib/theme.ts`) ho nastaví ešte pred vykreslením, aby stránka pri načítaní nezablikala.
- Tmavé farby sú v `globals.css` pod `:root[data-theme="dark"]`. Tailwind trieda `dark:` sa riadi tým istým atribútom.

## Next.js 16: na čo si dať pozor

Táto verzia má zapnuté **Cache Components** (`next.config.ts`). `params` a `searchParams` sú `Promise` a komponent, ktorý ich číta (alebo volá API), musí byť v `<Suspense>`. Inak dev server hlási, že navigácia nebude okamžitá. Vzor nájdeš v `src/app/inzeraty/page.tsx`. Aktuálna dokumentácia je v `node_modules/next/dist/docs/`.

Ak sa zmena v `src/app/globals.css` neprejaví (napr. nové farby), dev server ju nezachytil. Zastav ho, zmaž cache a spusti znova:

```bash
rm -rf .next && npm run dev
```
