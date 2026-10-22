using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vybavim.Common.Database;
using Vybavim.Common.Extensions;
using Vybavim.Modules.Identity.Application;
using Vybavim.Modules.Identity.Domain;
using Vybavim.Modules.Identity.Endpoints;
using Vybavim.Modules.Identity.Infrastructure;
using Vybavim.Modules.Identity.Infrastructure.Seeding;

namespace Vybavim.Modules.Identity;

/// <summary>
/// Vstupný bod modulu Identity: používatelia, heslá, role a prihlásenie cez ASP.NET Core Identity.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<UsersDbContext>(configuration, UsersDbContext.SchemaName);
        services.TryAddSingleton(TimeProvider.System);

        // --- Prihlásenie: overenie tokenu v hlavičke "Authorization: Bearer …" ---
        services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);
        services.AddAuthorization();

        // --- ASP.NET Core Identity: používatelia, heslá, role ---
        services
            .AddIdentityCore<User>(options =>
            {
                // Heslo: aspoň 8 znakov, veľké a malé písmeno, číslica. Špeciálny znak nevyžadujeme –
                // dĺžka je dôležitejšia ako zložitosť (aj podľa odporúčaní NIST).
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;

                // Po 5 zlých heslách sa účet na 15 minút zablokuje (ochrana pred hádaním hesla)
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<UsersDbContext>()   // ukladanie do PostgreSQL, schéma identity
            .AddSignInManager()
            .AddErrorDescriber<SlovakIdentityErrorDescriber>()
            .AddDefaultTokenProviders();                  // neskôr: overenie e-mailu, zabudnuté heslo

        // Prípady použitia
        services.AddScoped<RegisterUser>();
        services.AddScoped<LoginUser>();
        services.AddScoped<RefreshAccess>();
        services.AddScoped<GetCurrentUser>();
        services.AddScoped<GetUsers>();

        // Testovacie účty (admin) – spustí ich Api iba vo vývoji
        services.AddScoped<IDevDataSeeder, IdentityDevSeeder>();

        return services;
    }

    /// <summary>Zapne HTTP adresy modulu: /api/v1/auth/…, /api/v1/users</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app) =>
        IdentityEndpoints.Map(app);
}
