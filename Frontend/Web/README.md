# Web

Frontend platformy: **Next.js 16** (React, TypeScript), Tailwind CSS 4 a komponenty [shadcn/ui](https://ui.shadcn.com).

Biznis logika sem nepatrí. Web volá API cez typovaného klienta z [`Shared/`](../Shared/README.md).

> **Stav:** ukážková verzia na **mock dátach** (`src/lib/listings.ts`). Keď backend sprístupní `GET /api/v1/listings`, mock nahradí klient vygenerovaný z OpenAPI a stránky zostanú rovnaké.

## Spustenie

Verzia Node je v `.nvmrc` (Node 24). S `fnm` sa prepne automaticky pri vstupe do priečinka.

```bash
cd Frontend/Web
npm install
npm run dev
```

Web beží na http://localhost:3000.

| Príkaz              | Čo robí                                                      |
| ------------------- | ------------------------------------------------------------ |
| `npm run dev`       | vývojový server s hot reloadom                               |
| `npm run build`     | produkčný build (odhalí aj chyby pri predgenerovaní stránok) |
| `npm run lint`      | ESLint                                                       |
| `npm run typecheck` | kontrola typov                                               |
| `npm run format`    | naformátuje kód (Prettier + zoradenie Tailwind tried)        |

## Stránky

| URL                               | Súbor                            | Obsah                                                                                     |
| --------------------------------- | -------------------------------- | ----------------------------------------------------------------------------------------- |
| `/`                               | `src/app/page.tsx`               | úvod: vyhľadávanie, kategórie, najnovšie inzeráty, ako to funguje                         |
| `/inzeraty`                       | `src/app/inzeraty/page.tsx`      | zoznam s vyhľadávaním, kategóriami, panelom „Filtre“ a zoradením                          |
| `/inzeraty/[id]`                  | `src/app/inzeraty/[id]/page.tsx` | detail: fakty v dlaždiciach, výsledky, zručnosti, požiadavky, zadávateľ, podobné inzeráty |
| `/prihlasenie`, `/pridat-inzerat` |                                  | zatiaľ „pripravujeme“                                                                     |

### Filtre inzerátov (parametre v URL)

Filtre sú v URL, takže sa výsledok dá zdieľať odkazom. Prázdne polia sa do URL nepridávajú (`src/components/search-form.tsx`).

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

### Údaje inzerátu

Okrem názvu, popisu a rozpočtu má inzerát (typ `Listing` v `src/lib/listings.ts`) tieto polia, ktoré treba dohodnúť s backendom pre modul `Listings`:

| Pole                                                            | Povinné | Význam                                                           |
| --------------------------------------------------------------- | ------- | ---------------------------------------------------------------- |
| `level`                                                         | áno     | očakávaná úroveň: začiatočník / skúsený / expert                 |
| `scope`                                                         | áno     | veľkosť projektu (malý / stredný / veľký) a odhad trvania        |
| `engagement`                                                    | áno     | jednorazovo / dlhodobo                                           |
| `invoiceRequired`                                               | áno     | či zadávateľ potrebuje faktúru (živnosť alebo firma)             |
| `skills`                                                        | áno     | zručnosti ako štítky (vyhľadávanie, filtre, neskôr AI párovanie) |
| `deliverables`                                                  | nie     | čo má byť výsledkom                                              |
| `requirements`                                                  | nie     | požiadavky na odborníka (prax, portfólio, jazyk)                 |
| `owner.verified`, `projectCount`, `memberSince`, `responseTime` | áno     | dôveryhodnosť zadávateľa                                         |

Voliteľné sekcie sa na detaile zobrazia iba vtedy, keď sú vyplnené. Pri pridávaní inzerátu ich preto netreba vypĺňať, aby formulár neodradil. Prílohy, otázky pre uchádzačov a polia podľa kategórie (JSONB) prídu v ďalšej verzii.

### Ceny

Pevná cena a hodinová sadzba sa **nikdy navzájom neporovnávajú**. Keď je zadaný rozsah iba pre sadzbu, zobrazia sa len inzeráty s hodinovou sadzbou (a naopak). Pri zoradení podľa ceny idú najprv pevné ceny, potom hodinové sadzby a nakoniec „dohodou“. Logika je v `getListings()` v `src/lib/listings.ts`.

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

Táto verzia má zapnuté **Cache Components** (`next.config.ts`). `params` a `searchParams` sú `Promise` a komponent, ktorý ich číta, musí byť v `<Suspense>`. Inak dev server hlási, že navigácia nebude okamžitá. Vzor nájdeš v `src/app/inzeraty/page.tsx`. Aktuálna dokumentácia je v `node_modules/next/dist/docs/`.

Ak sa zmena v `src/app/globals.css` neprejaví (napr. nové farby), dev server ju nezachytil. Zastav ho, zmaž cache a spusti znova:

```bash
rm -rf .next && npm run dev
```
