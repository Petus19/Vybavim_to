using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Application;

/// <summary>Nastavenia prihlásenia cez Google (appsettings: Authentication:Google:ClientId).</summary>
internal sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    /// <summary>Client ID z Google Cloud Console. Nie je tajný – tajný je iba Client secret, ten má len web.</summary>
    public string? ClientId { get; set; }
}

/// <summary>
/// Prípad použitia: „prihlás používateľa cez Google“.
/// Web pošle ID token od Google; API overí jeho podpis a že bol vydaný pre našu aplikáciu,
/// nájde alebo vytvorí používateľa a vráti „preukaz“ – z neho endpoint vytvorí rovnaké tokeny ako pri hesle.
/// </summary>
internal sealed class LoginWithGoogle(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IOptions<GoogleAuthOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>Názov poskytovateľa v tabuľke user_logins.</summary>
    public const string Provider = "Google";

    public static readonly Error NotConfigured =
        Error.Validation("identity.google_not_configured", "Prihlásenie cez Google nie je nastavené.");

    public static readonly Error InvalidToken =
        Error.Unauthorized("identity.invalid_google_token", "Prihlásenie cez Google sa nepodarilo. Skúste to znova.");

    public static readonly Error EmailNotVerified =
        Error.Unauthorized("identity.google_email_not_verified", "E-mail v Google účte nie je overený.");

    public async Task<Result<ClaimsPrincipal>> HandleAsync(GoogleLoginRequest request)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Failure<ClaimsPrincipal>(NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Result.Failure<ClaimsPrincipal>(InvalidToken);
        }

        // 1. Overenie tokenu: podpis od Google, platnosť a že je pre NAŠU aplikáciu (audience = náš Client ID).
        //    Bez kontroly audience by sa dal použiť token, ktorý niekto získal pre úplne inú aplikáciu.
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            return Result.Failure<ClaimsPrincipal>(InvalidToken);
        }

        // Neoverenému e-mailu neveríme – inak by sa dal prevziať cudzí účet s rovnakým e-mailom
        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
        {
            return Result.Failure<ClaimsPrincipal>(EmailNotVerified);
        }

        // 2. Používateľ: už prepojený s týmto Google účtom → ten istý účet s rovnakým e-mailom → nový účet
        var user = await userManager.FindByLoginAsync(Provider, payload.Subject);
        if (user is null)
        {
            var linked = await LinkOrCreateAsync(payload);
            if (!linked.IsSuccess)
            {
                return Result.Failure<ClaimsPrincipal>(linked.Error!);
            }

            user = linked.Value;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<ClaimsPrincipal>(LoginUser.LockedOut);
        }

        // 3. Preukaz s ID, menom a rolami – rovnaký ako pri prihlásení heslom
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return Result.Success(principal);
    }

    /// <summary>
    /// Prvé prihlásenie cez tento Google účet: ak už existuje účet s rovnakým (overeným) e-mailom,
    /// pripojí k nemu Google; inak vytvorí nový účet bez hesla.
    /// </summary>
    private async Task<Result<User>> LinkOrCreateAsync(GoogleJsonWebSignature.Payload payload)
    {
        var email = payload.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            var now = timeProvider.GetUtcNow();
            user = new User
            {
                Id = Guid.CreateVersion7(now),
                UserName = email,
                Email = email,
                EmailConfirmed = true,   // Google e-mail overil
                CreatedAt = now,
            };

            var created = await userManager.CreateAsync(user);   // bez hesla – prihlasuje sa cez Google
            if (!created.Succeeded)
            {
                return Failure(created);
            }
        }

        var login = await userManager.AddLoginAsync(user, new UserLoginInfo(Provider, payload.Subject, Provider));
        return login.Succeeded ? Result.Success(user) : Failure(login);
    }

    private static Result<User> Failure(IdentityResult result) =>
        Result.Failure<User>(Error.Validation(
            "identity.google_login_failed",
            string.Join(" ", result.Errors.Select(e => e.Description))));
}
