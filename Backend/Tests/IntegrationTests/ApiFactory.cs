using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

// Jedna inštancia ApiFactory (a teda jeden PostgreSQL kontajner) pre všetky testy v projekte.
[assembly: AssemblyFixture(typeof(Vybavim.IntegrationTests.ApiFactory))]

namespace Vybavim.IntegrationTests;

/// <summary>
/// Spustí celé Api ako pri `dotnet run`, ale proti dočasnému PostgreSQL v Dockeri.
/// Api pri štarte (prostredie Development) aplikuje migrácie a vloží testovacie dáta,
/// takže testy majú vždy rovnakých 12 inzerátov a 4 kategórie.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Rovnaká verzia PostgreSQL ako v docker-compose.yml
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17.11").Build();

    /// <summary>Spustí sa raz pred všetkými testami: naštartuje databázu v Dockeri.</summary>
    public async ValueTask InitializeAsync() => await _database.StartAsync();

    /// <summary>Spustí sa raz po všetkých testoch: zastaví Api a zmaže kontajner.</summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Namiesto databázy z appsettings.Development.json použi dočasnú testovaciu
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
    }
}
