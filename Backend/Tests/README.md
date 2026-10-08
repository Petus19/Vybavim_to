# Tests

Testovacie projekty sa pridajú spolu s prvým kódom (Fáza 0):

| Projekt | Obsah |
|---|---|
| `Vybavim.Modules.<Modul>.Tests` | unit testy domény a prípadov použitia modulu (vidí jeho `internal` typy cez `InternalsVisibleTo`) |
| `Vybavim.IntegrationTests` | testy API proti skutočnému PostgreSQL a Redisu cez Testcontainers |
| `Vybavim.ArchitectureTests` | vynútenie pravidiel: modul referencuje iné moduly iba cez `.Contracts`, `Domain` nezávisí od `Infrastructure`, implementácia je `internal` |
