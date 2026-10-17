namespace Vybavim.Common.Database;

/// <summary>
/// Naplní databázu modulu testovacími dátami. Spúšťa sa IBA vo vývoji, nikdy v produkcii.
/// Každý modul si môže zaregistrovať vlastný seeder; spustí ich
/// <see cref="Extensions.DatabaseExtensions.SeedDevDataAsync"/> po migráciách.
/// </summary>
public interface IDevDataSeeder
{
    /// <summary>
    /// Vloží testovacie dáta. Musí byť bezpečné spustiť ho opakovane
    /// (keď dáta už existujú, nerobí nič).
    /// </summary>
    public Task SeedAsync(CancellationToken cancellationToken = default);
}
