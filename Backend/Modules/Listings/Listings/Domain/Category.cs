namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Kategória inzerátov, napr. "Web a aplikácie" → podkategória "E-shopy".
/// Kategórie tvoria strom: podkategória má vyplnené ParentId.
/// </summary>
internal sealed class Category(Guid id, string slug, string name, string description, Guid? parentId, int sortOrder)
{
    public const int SlugMaxLength = 60;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 200;

    public Guid Id { get; private set; } = id;

    /// <summary>Strojový názov do URL, napr. "grafika". Je unikátny.</summary>
    public string Slug { get; private set; } = slug;

    /// <summary>Názov pre ľudí, napr. "Grafika a logo".</summary>
    public string Name { get; private set; } = name;

    /// <summary>Krátky popis na dlaždici kategórie, napr. "Logá, vizuály, tlačoviny".</summary>
    public string Description { get; private set; } = description;

    /// <summary>Nadradená kategória. Null = hlavná kategória.</summary>
    public Guid? ParentId { get; private set; } = parentId;

    /// <summary>Poradie pri zobrazení (menšie číslo = vyššie).</summary>
    public int SortOrder { get; private set; } = sortOrder;
}
