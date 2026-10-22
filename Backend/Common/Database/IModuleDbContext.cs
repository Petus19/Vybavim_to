namespace Vybavim.Common.Database;

/// <summary>
/// Databáza modulu – čokoľvek, čo má vlastnú PostgreSQL schému.
/// Väčšina modulov dedí z <see cref="BaseDbContext"/>; modul Identity dedí z IdentityDbContext
/// (ASP.NET Core Identity), preto spoločná je iba táto zmluva, nie základná trieda.
/// </summary>
public interface IModuleDbContext
{
    /// <summary>Názov PostgreSQL schémy modulu, napr. "listings" alebo "identity".</summary>
    public string Schema { get; }
}
