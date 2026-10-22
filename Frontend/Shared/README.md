# Shared

TypeScript kód zdieľaný medzi `Web` a `Mobile`: typy API vygenerované z OpenAPI backendu.

Backend je v C#, preto sa typy s ním nezdieľajú ručne. Zdrojom pravdy je OpenAPI špecifikácia z `Backend/Api` (`http://localhost:5080/openapi/v1.json`).

| Súbor | Obsah |
|---|---|
| `api/schema.d.ts` | typy všetkých endpointov a odpovedí API – **generovaný súbor, needitovať ručne** |

## Pregenerovanie typov

Keď sa v backende zmení API (nový endpoint, pole, enum), treba typy pregenerovať a commitnúť:

```bash
cd Backend && dotnet run --project Api      # API musí bežať (OpenAPI je iba vo vývoji)
cd Frontend/Web && npm run api:types        # prepíše Frontend/Shared/api/schema.d.ts
```

Potom `npm run typecheck` vo `Frontend/Web` ukáže každé miesto na webe, ktoré treba upraviť. Súbor je v gite, takže CI ani kolega na zostavenie webu nepotrebujú bežiace API.
