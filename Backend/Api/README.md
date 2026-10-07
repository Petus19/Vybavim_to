# Api

Spúšťací projekt backendu: REST API (`/api/v1/…`, OpenAPI) + WebSockety.

Neobsahuje biznis logiku, iba zapojí spoločný kód z `Common/` a jednotlivé moduly z `Modules/`:

```csharp
builder.Services.AddPostgres(config);        // Common/Extensions
builder.Services.AddListingsModule(config);  // Modules/Listings
builder.Services.AddPaymentsModule(config);  // Modules/Payments
```
