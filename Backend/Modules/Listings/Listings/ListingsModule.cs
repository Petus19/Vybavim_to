using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vybavim.Common.Database;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Listings.Application;
using Vybavim.Modules.Listings.Endpoints;
using Vybavim.Modules.Listings.Infrastructure;
using Vybavim.Modules.Listings.Infrastructure.Seeding;

namespace Vybavim.Modules.Listings;

/// <summary>
/// Vstupný bod modulu Listings. Jediná verejná trieda v module:
/// Api a Worker cez ňu modul zapoja, nič iné z modulu nevidia.
/// </summary>
public static class ListingsModule
{
    /// <summary>Zaregistruje služby modulu (databáza, prípady použitia, testovacie dáta).</summary>
    public static IServiceCollection AddListingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ListingsDbContext>(configuration, ListingsDbContext.SchemaName);
        services.TryAddSingleton(TimeProvider.System);   // hodiny pre čas vytvorenia a úpravy

        // Prípady použitia – endpointy si ich vypýtajú cez Dependency Injection
        services.AddScoped<GetCategories>();
        services.AddScoped<SearchListings>();
        services.AddScoped<GetListingDetail>();
        services.AddScoped<CreateListing>();
        services.AddScoped<UpdateListing>();
        services.AddScoped<CancelListing>();
        services.AddScoped<GetMyListings>();

        // Testovacie inzeráty – spustí ich Api iba vo vývoji (SeedDevDataAsync).
        services.AddScoped<IDevDataSeeder, ListingsDevSeeder>();

        return services;
    }

    /// <summary>Zapne HTTP adresy modulu: /api/v1/categories, /api/v1/listings…</summary>
    public static IEndpointRouteBuilder MapListingsEndpoints(this IEndpointRouteBuilder app) =>
        ListingsEndpoints.Map(app);
}
