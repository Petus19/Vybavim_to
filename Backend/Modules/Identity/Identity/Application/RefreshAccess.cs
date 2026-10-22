using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Application;

/// <summary>
/// Prípad použitia: „vydaj nový prístupový token podľa refresh tokenu“.
/// Prístupový token platí krátko (1 hodinu); refresh token dlhšie (14 dní) a slúži iba na získanie nového.
/// </summary>
internal sealed class RefreshAccess(
    SignInManager<User> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    TimeProvider timeProvider)
{
    public static readonly Error InvalidToken =
        Error.Unauthorized("identity.invalid_refresh_token", "Prihlásenie vypršalo. Prihláste sa znova.");

    public async Task<Result<ClaimsPrincipal>> HandleAsync(RefreshRequest request)
    {
        // Refresh token je zašifrovaný „lístok“ – rozšifrovať ho vie iba naše API
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        // Platný musí byť lístok, jeho dátum aj „pečiatka“ používateľa – po zmene hesla
        // sa pečiatka zmení a staré refresh tokeny prestanú fungovať.
        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc
            || timeProvider.GetUtcNow() >= expiresUtc
            || await signInManager.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
        {
            return Result.Failure<ClaimsPrincipal>(InvalidToken);
        }

        // Nový preukaz s aktuálnymi rolami (ak medzitým používateľ dostal/stratil rolu)
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return Result.Success(principal);
    }
}
