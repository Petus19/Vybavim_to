# Shared

TypeScript code shared by `Web` and (later) `Mobile`: API types generated from the backend's OpenAPI document.

The backend is written in C#, so types are not shared by hand – the source of truth is the OpenAPI document produced by `Backend/Api` (`http://localhost:5080/openapi/v1.json`).

| File | Contents |
|---|---|
| `api/schema.d.ts` | types of all API endpoints and responses – **generated, do not edit by hand** |

## Regenerating the types

When the API changes (new endpoint, field or enum value), regenerate the types and commit them:

```bash
cd Backend && dotnet run --project Api      # the API must be running (OpenAPI is exposed in development)
cd Frontend/Web && npm run api:types        # overwrites Frontend/Shared/api/schema.d.ts
```

`npm run typecheck` in `Frontend/Web` then points at every place in the web app that needs updating. The file is committed, so CI and teammates can build the web app without a running API.
