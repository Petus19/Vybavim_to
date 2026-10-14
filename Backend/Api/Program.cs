using Vybavim.Common.Extensions;
using Vybavim.Modules.Listings;

var builder = WebApplication.CreateBuilder(args);

// --- Moduly ---
builder.Services.AddListingsModule(builder.Configuration);

var app = builder.Build();

// Vo vývoji sa databáza pri štarte sama aktualizuje na najnovšiu verziu.
if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabasesAsync();
}

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();
