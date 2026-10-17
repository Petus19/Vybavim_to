using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vybavim.Common.Database;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Listings.Infrastructure;
using Vybavim.Modules.Listings.Infrastructure.Seeding;

namespace Vybavim.Modules.Listings;

/// <summary>
/// Vstupný bod modulu Listings. Jediná verejná trieda v module:
/// Api a Worker cez ňu modul zapoja, nič iné z modulu nevidia.
/// </summary>
public static class ListingsModule
{
    public static IServiceCollection AddListingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ListingsDbContext>(configuration, ListingsDbContext.SchemaName);

        // Testovacie inzeráty – spustí ich Api iba vo vývoji (SeedDevDataAsync).
        services.AddScoped<IDevDataSeeder, ListingsDevSeeder>();

        return services;
    }
}
