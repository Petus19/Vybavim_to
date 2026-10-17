namespace Vybavim.Modules.Listings.Infrastructure.Seeding;

/// <summary>
/// Pevné ID hlavných kategórií. Používa ich migrácia (HasData) aj testovacie dáta.
/// Nikdy ich nemeniť – sú uložené v databázach u všetkých vývojárov aj na serveri.
/// </summary>
internal static class CategoryIds
{
    public static readonly Guid Web = Guid.Parse("019a0b2c-1000-7000-8000-000000000001");
    public static readonly Guid Graphics = Guid.Parse("019a0b2c-1000-7000-8000-000000000002");
    public static readonly Guid ItSupport = Guid.Parse("019a0b2c-1000-7000-8000-000000000003");
    public static readonly Guid Marketing = Guid.Parse("019a0b2c-1000-7000-8000-000000000004");
}
