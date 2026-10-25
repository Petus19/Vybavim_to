using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Infrastructure;

/// <summary>Databáza modulu Profiles. Vidí iba tabuľky v schéme "profiles".</summary>
internal sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : BaseDbContext(options)
{
    public const string SchemaName = "profiles";

    public override string Schema => SchemaName;

    public DbSet<Profile> Profiles => Set<Profile>();
}
