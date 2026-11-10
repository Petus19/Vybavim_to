using Microsoft.AspNetCore.Identity;
using Vybavim.Common.Database;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Demo účty pre AI agenta a jeho vyhodnotenie (heslo všetkých: Demo1234):
/// client.demo – zadávateľ s inzerátmi a ponukami na ne; specialist.demo, specialist2.demo,
/// specialist3.demo – špecialisti, ktorí posielajú ponuky.
/// Registruje sa iba vo vývoji so zapnutým <see cref="DevData.DemoAccountsKey"/>.
/// ID sa musia zhodovať s ProfilesDemoSeeder, ListingsDemoSeeder a ProjectsDemoSeeder.
/// </summary>
internal sealed class IdentityDemoSeeder(UserManager<User> userManager, TimeProvider timeProvider) : IDevDataSeeder
{
    internal const string Password = "Demo1234";

    internal static readonly (Guid Id, string UserName)[] Accounts =
    [
        (Guid.Parse("019a0b2c-3000-7000-8000-000000000001"), "client.demo"),
        (Guid.Parse("019a0b2c-3000-7000-8000-000000000002"), "specialist.demo"),
        (Guid.Parse("019a0b2c-3000-7000-8000-000000000003"), "specialist2.demo"),
        (Guid.Parse("019a0b2c-3000-7000-8000-000000000004"), "specialist3.demo"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (id, userName) in Accounts)
        {
            if (await userManager.FindByIdAsync(id.ToString()) is not null)
            {
                continue;
            }

            var user = new User
            {
                Id = id,
                UserName = userName,
                Email = $"{userName}@vybavim.local",
                EmailConfirmed = true,
                CreatedAt = timeProvider.GetUtcNow(),
            };

            // Rovnaké overenie hesla ako pri registrácii
            var result = await userManager.CreateAsync(user, Password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Nepodarilo sa vytvoriť demo účet: " + string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
