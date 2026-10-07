# Modules

Moduly modulárneho monolitu. Každý modul má všetko svoje na jednom mieste a tieto vrstvy:

| Vrstva | Obsah |
|---|---|
| `PublicApi/` | REST endpointy `/api/v1/…` pre web a mobil, validácia vstupu |
| `InternalApi/` | rozhranie pre ostatné moduly (volanie v rámci procesu, nie HTTP) |
| `Application/` | prípady použitia („pridaj inzerát“, „odpovedz na inzerát“) |
| `Domain/` | biznis pravidlá (napr. recenzia iba k dokončenému projektu) |
| `Infrastructure/` | DbContext modulu (iba jeho tabuľky), externé služby |

**Pravidlá:**

- Modul si nesiaha do cudzích tabuliek ani tried. S iným modulom komunikuje iba cez jeho `InternalApi/` alebo cez udalosti (outbox pattern).
- Spoločný technický kód (DB, auth, chyby, extensions) je v [`Common/`](../Common/README.md), nie v moduloch.
- Každý modul má extension metódu na registráciu (napr. `AddListingsModule()`), ktorú zavolá `Api`.

Podrobnosti sú v [hlavnom README](../../README.md#architektúra).
