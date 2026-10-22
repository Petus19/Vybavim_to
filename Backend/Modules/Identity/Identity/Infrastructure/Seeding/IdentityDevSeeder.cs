using Microsoft.AspNetCore.Identity;
using Vybavim.Common.Auth;
using Vybavim.Common.Database;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Testovacie účty pre vývoj: rola Admin a admin s menom "Admin" a heslom "Admin123".
/// Spúšťa sa IBA vo vývoji (SeedDevDataAsync) – na produkčnom serveri tento účet nikdy nevznikne.
/// </summary>
internal sealed class IdentityDevSeeder(
    UserManager<User> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    TimeProvider timeProvider) : IDevDataSeeder
{
    private const string AdminUserName = "Admin";
    private const string AdminEmail = "admin@vybavim.local";
    private const string AdminPassword = "Admin123";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Rola Admin (ak ešte neexistuje)
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin)));
        }

        // 2. Admin účet (ak ešte neexistuje) – rovnaké overenie hesla ako pri registrácii
        if (await userManager.FindByNameAsync(AdminUserName) is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var admin = new User
        {
            Id = Guid.CreateVersion7(now),
            UserName = AdminUserName,
            Email = AdminEmail,
            EmailConfirmed = true,
            CreatedAt = now,
        };

        EnsureSucceeded(await userManager.CreateAsync(admin, AdminPassword));
        EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.Admin));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Nepodarilo sa vytvoriť testovacie účty: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
