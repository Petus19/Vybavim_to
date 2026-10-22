using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Infrastructure;

/// <summary>
/// Databáza modulu Identity (schéma "identity"). Dedí z IdentityDbContext, ktorý
/// pripraví tabuľky ASP.NET Core Identity: používatelia, role, priradenia rolí, prihlásenia cez Google…
/// Nedá sa dediť z BaseDbContext, preto implementuje iba spoločnú zmluvu IModuleDbContext.
/// </summary>
internal sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IModuleDbContext
{
    public const string SchemaName = "identity";

    public string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Najprv Identity: vytvorí model svojich tabuliek, kľúče a indexy
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(SchemaName);

        // Kratšie názvy tabuliek namiesto predvolených "AspNetUsers", "AspNetRoles"…
        builder.Entity<User>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
