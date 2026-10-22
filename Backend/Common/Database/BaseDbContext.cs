using Microsoft.EntityFrameworkCore;

namespace Vybavim.Common.Database;

/// <summary>
/// Spoločný základ pre DbContext každého modulu.
/// Každý modul má vlastnú PostgreSQL schému (napr. "listings"), takže jeho tabuľky
/// sú oddelené od tabuliek iných modulov.
/// </summary>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options), IModuleDbContext
{
    /// <summary>Názov PostgreSQL schémy modulu, napr. "listings".</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Všetky tabuľky modulu pôjdu do jeho schémy: listings.categories, listings.listings…
        modelBuilder.HasDefaultSchema(Schema);

        // Konfigurácie entít (IEntityTypeConfiguration) sa načítajú automaticky
        // z projektu modulu, ktorý od tejto triedy dedí.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Všetky decimal (peniaze): 12 číslic, z toho 2 desatinné → max. 9 999 999 999,99.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }
}
