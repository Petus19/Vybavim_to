# Common

Spoločný **technický** kód pre všetky moduly. Píše sa raz, aby sa v moduloch neduplikoval.

| Priečinok | Obsah |
|---|---|
| `Database/` | `BaseDbContext`, pripojenie k DB, outbox |
| `Auth/` | JWT, oprávnenia |
| `Events/` | doménové udalosti, fronta |
| `Errors/` | jednotné spracovanie chýb |
| `Extensions/` | extension metódy (`AddPostgres`, `AddRedis`, …) |

> ⚠️ Biznis logika sem **nepatrí**. Entity ako `User` alebo pravidlá pre inzeráty zostávajú vo svojom module. Ak modul potrebuje dáta iného modulu, získa ich cez jeho `.Contracts` projekt.
