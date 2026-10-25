using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vybavim.Common.Database;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Profiles.Application;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Endpoints;
using Vybavim.Modules.Profiles.Infrastructure;
using Vybavim.Modules.Profiles.Infrastructure.Seeding;

namespace Vybavim.Modules.Profiles;

/// <summary>Vstupný bod modulu Profiles: verejné profily používateľov (jednotlivec / firma).</summary>
public static class ProfilesModule
{
    public static IServiceCollection AddProfilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProfilesDbContext>(configuration, ProfilesDbContext.SchemaName);
        services.TryAddSingleton(TimeProvider.System);

        // Prípady použitia
        services.AddScoped<GetProfile>();
        services.AddScoped<SaveMyProfile>();

        // Kontrakt pre ostatné moduly: kto si vypýta IProfilesApi, dostane našu internal implementáciu
        services.AddScoped<IProfilesApi, ProfilesApi>();

        // Testovacie profily – spustí ich Api iba vo vývoji
        services.AddScoped<IDevDataSeeder, ProfilesDevSeeder>();

        return services;
    }

    /// <summary>Zapne HTTP adresy modulu: /api/v1/profiles/…</summary>
    public static IEndpointRouteBuilder MapProfilesEndpoints(this IEndpointRouteBuilder app) =>
        ProfilesEndpoints.Map(app);
}
