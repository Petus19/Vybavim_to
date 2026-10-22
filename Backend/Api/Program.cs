using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Vybavim.Api.OpenApi;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Identity;
using Vybavim.Modules.Listings;
using Vybavim.Modules.Profiles;

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

    // Prísne čítanie požiadaviek: chýbajúce povinné pole (napr. "remote") alebo null tam,
    // kde nesmie byť (napr. "title"), je chyba 400 – nie potichu dosadená predvolená hodnota.
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.RespectNullableAnnotations = true;
});

// Popis API (OpenAPI) – zo všetkých endpointov sa automaticky vytvorí /openapi/v1.json.
// Transformer doplní prihlasovanie tokenom, aby sa dalo skúšať aj v Scalar.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Web beží na inej adrese (localhost:3000) – prehliadač mu musí dovoliť volať API.
// Povolené adresy sú v appsettings (Cors:AllowedOrigins).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// „Kto práve volá API“ – moduly si ho vypýtajú cez ICurrentUser
builder.Services.AddCurrentUser();

// --- Moduly ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddProfilesModule(builder.Configuration);
builder.Services.AddListingsModule(builder.Configuration);

var app = builder.Build();

// Nečakaná chyba (výnimka) → 500 vo formáte ProblemDetails, bez detailov kódu pre klienta.
// Výnimka: zlá požiadavka (napr. chýbajúce povinné pole v JSON) – ASP.NET ju vo vývoji hlási
// ako BadHttpRequestException so stavom 400; ten zachováme, inak by z nej bola chybne 500.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseCors();

// Prihlásenie: z hlavičky "Authorization: Bearer …" zistí, kto volá (Authentication),
// a pri chránených endpointoch overí, či na to má právo (Authorization).
app.UseAuthentication();
app.UseAuthorization();

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

app.MapIdentityEndpoints();
app.MapProfilesEndpoints();
app.MapListingsEndpoints();

await app.RunAsync();
