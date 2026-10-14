using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Infrastructure;

/// <summary>
/// Databáza modulu Listings. Vidí iba tabuľky v schéme "listings".
/// Je internal: žiadny iný modul sa k nej nedostane.
/// </summary>
internal sealed class ListingsDbContext(DbContextOptions<ListingsDbContext> options) : BaseDbContext(options)
{
    public const string SchemaName = "listings";

    public override string Schema => SchemaName;

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
}
