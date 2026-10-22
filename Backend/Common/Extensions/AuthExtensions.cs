using Microsoft.Extensions.DependencyInjection;
using Vybavim.Common.Auth;

namespace Vybavim.Common.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Zaregistruje <see cref="ICurrentUser"/> – moduly si ním zistia, kto volá API.
    /// Samotné prihlásenie (tokeny, heslá) rieši modul Identity.
    /// </summary>
    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
