# Modules

Moduly modulárneho monolitu. Každý modul tvoria **dva projekty**:

```
Listings/
├── Listings/                 # Vybavim.Modules.Listings – implementácia, všetko internal
│   ├── Endpoints/            # REST endpointy /api/v1/… pre web a mobil (Minimal APIs)
│   ├── Application/          # prípady použitia („pridaj inzerát“, „odpovedz na inzerát“)
│   ├── Domain/               # entity a biznis pravidlá
│   └── Infrastructure/       # DbContext modulu (iba jeho schéma), migrácie, externé služby
└── Listings.Contracts/       # Vybavim.Modules.Listings.Contracts – kontrakt pre ostatné moduly
                              # rozhrania, DTO, integračné udalosti
```

**Pravidlá:**

- Modul referencuje iné moduly **iba cez ich `.Contracts` projekt**, nikdy cez implementáciu. Tak nevzniknú cyklické závislosti (napr. Projects ↔ Listings) a hranicu stráži kompilátor.
- `.Contracts` nereferencuje žiadny iný projekt.
- Typy v implementácii sú `internal`. Verejná je iba registrácia modulu (`AddListingsModule()`, `MapListingsEndpoints()`), ktorú zavolá `Api` a `Worker`.
- Každý modul má **vlastnú PostgreSQL schému** (`HasDefaultSchema("listings")`) a vlastnú históriu migrácií v `Infrastructure/Migrations/`.
- Spoločný technický kód (DB, auth, chyby, udalosti) je v [`Common/`](../Common/README.md), nie v moduloch.
- Moduly, ktoré pracujú s dátami všetkých ostatných (`Gdpr`, `Admin`), to robia cez rozhrania v `.Contracts`. Napríklad každý modul implementuje rozhranie na export a anonymizáciu osobných údajov a `Gdpr` ich iba zavolá.

**Vyhľadávanie** je v MVP súčasťou modulu `Listings`. Samostatný modul `Search` s vlastným read modelom vznikne až so sémantickým vyhľadávaním (pgvector).

Podrobnosti sú v [hlavnom README](../../README.md#architektúra).
