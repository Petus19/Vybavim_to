using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Listings.Infrastructure;

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

        return services;
    }
}
