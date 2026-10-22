using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Listings;

var builder = WebApplication.CreateBuilder(args);

// --- Spoločné nastavenia API ---

// Chyby vo formáte ProblemDetails (RFC 9457): { "status": 404, "title": "…", … }
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enumy v JSON ako text v tvare snake_case: InProgress → "in_progress", OneTime → "one_time"
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

    // Čísla iba ako čísla. Predvolene ASP.NET prijíma aj "300" ako text a OpenAPI potom
    // popisuje každé číslo ako „number alebo string“ – vygenerované typy na webe by boli nepresné.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Popis API (OpenAPI) – zo všetkých endpointov sa automaticky vytvorí /openapi/v1.json
builder.Services.AddOpenApi();

// Web beží na inej adrese (localhost:3000) – prehliadač mu musí dovoliť volať API.
// Povolené adresy sú v appsettings (Cors:AllowedOrigins).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// --- Moduly ---
builder.Services.AddListingsModule(builder.Configuration);

var app = builder.Build();

// Nečakaná chyba (výnimka) → 500 vo formáte ProblemDetails, bez detailov kódu pre klienta
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    // Databáza sa pri štarte sama aktualizuje na najnovšiu verziu
    // a naplní testovacími dátami (iba ak sú tabuľky prázdne).
    await app.MigrateDatabasesAsync();
    await app.SeedDevDataAsync();

    // Dokumentácia API: http://localhost:5080/scalar
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Systém")
    .WithSummary("Kontrola, či API beží");

app.MapListingsEndpoints();

await app.RunAsync();
