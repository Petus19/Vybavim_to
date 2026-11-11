using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vybavim.Common.Database;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Projects.Application;
using Vybavim.Modules.Projects.Endpoints;
using Vybavim.Modules.Projects.Infrastructure;
using Vybavim.Modules.Projects.Infrastructure.Seeding;

namespace Vybavim.Modules.Projects;

/// <summary>
/// Vstupný bod modulu Projects: ponuky odborníkov na inzeráty a ich prijatie.
/// (Neskôr sem pribudne samotný projekt – dohoda, odovzdanie, dokončenie.)
/// </summary>
public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProjectsDbContext>(configuration, ProjectsDbContext.SchemaName);
        services.TryAddSingleton(TimeProvider.System);

        // Prípady použitia
        services.AddScoped<SendOffer>();
        services.AddScoped<GetListingOffers>();
        services.AddScoped<GetMyOffers>();
        services.AddScoped<AcceptOffer>();
        services.AddScoped<RejectOffer>();
        services.AddScoped<WithdrawOffer>();
        services.AddScoped<OfferResponseBuilder>();

        if (DevData.DemoAccountsEnabled(configuration))
        {
            // Ponuky pre demo účty AI agenta; tá istá inštancia ich vie aj vrátiť do východiskového stavu
            services.AddScoped<ProjectsDemoSeeder>();
            services.AddScoped<IDevDataSeeder>(sp => sp.GetRequiredService<ProjectsDemoSeeder>());
            services.AddScoped<IDevDataResetter>(sp => sp.GetRequiredService<ProjectsDemoSeeder>());
        }

        return services;
    }

    /// <summary>Zapne HTTP adresy modulu: /api/v1/listings/{id}/offers, /api/v1/offers/…</summary>
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app) =>
        OffersEndpoints.Map(app);
}
