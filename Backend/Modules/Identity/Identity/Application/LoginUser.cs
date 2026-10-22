using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Application;

/// <summary>
/// Prípad použitia: „over prihlasovacie údaje“. Pri úspechu vráti ClaimsPrincipal – „preukaz“
/// s ID, menom a rolami používateľa. Endpoint z neho nechá ASP.NET vytvoriť token.
/// </summary>
internal sealed class LoginUser(UserManager<User> userManager, SignInManager<User> signInManager)
{
    // Rovnaká správa pri neznámom mene aj zlom hesle – útočník tak nezistí, či účet existuje.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.invalid_credentials", "Nesprávne prihlasovacie meno alebo heslo.");

    public static readonly Error LockedOut =
        Error.Unauthorized("identity.locked_out", "Príliš veľa neúspešných pokusov. Skúste to znova o 15 minút.");

    public async Task<Result<ClaimsPrincipal>> HandleAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<ClaimsPrincipal>(InvalidCredentials);
        }

        // Login môže byť používateľské meno ("Admin") alebo e-mail
        var login = request.Login.Trim();
        var user = await userManager.FindByNameAsync(login) ?? await userManager.FindByEmailAsync(login);
        if (user is null)
        {
            return Result.Failure<ClaimsPrincipal>(InvalidCredentials);
        }

        // Overí heslo; pri zlom hesle zvýši počítadlo a po 5 pokusoch účet dočasne zablokuje
        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Result.Failure<ClaimsPrincipal>(LockedOut);
        }

        if (!check.Succeeded)
        {
            return Result.Failure<ClaimsPrincipal>(InvalidCredentials);
        }

        // Preukaz používateľa: ID, meno, e-mail a role (napr. Admin)
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return Result.Success(principal);
    }
}
