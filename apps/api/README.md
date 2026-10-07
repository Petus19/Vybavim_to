# api

Backend ako **modulárny monolit**: REST API (`/api/v1/…`, OpenAPI) + WebSockety. Jazyk (NestJS / ASP.NET Core) zatiaľ nie je rozhodnutý.

Každý modul v `src/modules/` má tieto vrstvy:

| Vrstva | Obsah |
|---|---|
| `api/` | kontroléry, validácia vstupu |
| `application/` | prípady použitia („pridaj inzerát“, „odpovedz na inzerát“) |
| `domain/` | biznis pravidlá |
| `infrastructure/` | databáza, externé služby |

Moduly si nesiahajú do tabuliek. Komunikujú iba cez verejné rozhranie alebo udalosti (outbox pattern). Podrobnosti sú v [hlavnom README](../../README.md#architektúra).
