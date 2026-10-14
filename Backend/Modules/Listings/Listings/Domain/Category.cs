namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Kategória inzerátov, napr. "IT a programovanie" → podkategória "Webové stránky".
/// Kategórie tvoria strom: podkategória má vyplnené ParentId.
/// </summary>
internal sealed class Category(Guid id, string slug, string name, Guid? parentId, int sortOrder)
{
    public const int SlugMaxLength = 60;
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; } = id;

    /// <summary>Strojový názov do URL, napr. "webove-stranky". Je unikátny.</summary>
    public string Slug { get; private set; } = slug;

    /// <summary>Názov pre ľudí, napr. "Webové stránky".</summary>
    public string Name { get; private set; } = name;

    /// <summary>Nadradená kategória. Null = hlavná kategória.</summary>
    public Guid? ParentId { get; private set; } = parentId;

    /// <summary>Poradie pri zobrazení (menšie číslo = vyššie).</summary>
    public int SortOrder { get; private set; } = sortOrder;
}
