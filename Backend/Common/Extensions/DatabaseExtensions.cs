using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vybavim.Common.Database;

namespace Vybavim.Common.Extensions;

public static class DatabaseExtensions
{
    /// <summary>Názov connection stringu v appsettings.json (sekcia ConnectionStrings).</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>
    /// Zaregistruje DbContext modulu: pripojenie k PostgreSQL, vlastnú schému
    /// a migrátor, ktorý pri štarte aktualizuje databázu.
    /// Volá ho každý modul vo svojej metóde Add…Module().
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : DbContext, IModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Chýba connection string '{ConnectionStringName}' v konfigurácii (appsettings).");

        services.AddDbContext<TContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                // Každý modul si eviduje svoje migrácie samostatne vo svojej schéme.
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            // CategoryId → category_id, ListingStatus → listing_status…
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator<TContext>>();

        return services;
    }

    /// <summary>
    /// Spustí migrácie všetkých zaregistrovaných modulov. Volá sa pri štarte Api,
    /// iba vo vývojovom prostredí (v produkcii pôjdu migrácie samostatným krokom).
    /// </summary>
    public static async Task MigrateDatabasesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();

        foreach (var migrator in scope.ServiceProvider.GetServices<IDatabaseMigrator>())
        {
            await migrator.MigrateAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Spustí seedery testovacích dát všetkých modulov. Volá sa pri štarte Api
    /// IBA vo vývojovom prostredí, vždy až po <see cref="MigrateDatabasesAsync"/>.
    /// </summary>
    public static async Task SeedDevDataAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();

        foreach (var seeder in scope.ServiceProvider.GetServices<IDevDataSeeder>())
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
