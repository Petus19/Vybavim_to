namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Inzerát: zadávateľ hľadá odborníka na prácu.
/// Vlastnosti sa dajú meniť iba metódami tejto triedy (private set),
/// takže biznis pravidlá nikto nemôže obísť.
/// </summary>
internal sealed class Listing
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 5000;
    public const int CityMaxLength = 100;

    // Konštruktor je private: zvonku sa inzerát dá vytvoriť iba cez Create(), ktorý overí pravidlá.
    // EF Core ho pri načítaní z databázy použije tiež (názvy parametrov = názvy vlastností).
    private Listing(
        Guid id,
        Guid ownerId,
        Guid categoryId,
        string title,
        string description,
        decimal budgetAmount,
        string currency,
        DateOnly? deadline,
        string? city,
        ListingStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OwnerId = ownerId;
        CategoryId = categoryId;
        Title = title;
        Description = description;
        BudgetAmount = budgetAmount;
        Currency = currency;
        Deadline = deadline;
        City = city;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Kto inzerát zadal. ID používateľa z modulu Identity (zatiaľ bez väzby).</summary>
    public Guid OwnerId { get; private set; }

    public Guid CategoryId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }

    /// <summary>Rozpočet. Peniaze sú vždy decimal, nikdy double (double počíta nepresne).</summary>
    public decimal BudgetAmount { get; private set; }

    /// <summary>Mena podľa ISO 4217, napr. "EUR".</summary>
    public string Currency { get; private set; }

    /// <summary>Termín dokončenia. Null = bez termínu.</summary>
    public DateOnly? Deadline { get; private set; }

    /// <summary>Mesto. Null = práca na diaľku.</summary>
    public string? City { get; private set; }

    public ListingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Vytvorí nový zverejnený inzerát. Jediný spôsob, ako inzerát vznikne,
    /// takže tu sú všetky pravidlá pre nový inzerát na jednom mieste.
    /// </summary>
    public static Listing Create(
        Guid ownerId,
        Guid categoryId,
        string title,
        string description,
        decimal budgetAmount,
        DateOnly? deadline,
        string? city,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(title.Length, TitleMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Length, DescriptionMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetAmount);

        return new Listing(
            id: Guid.CreateVersion7(now),   // ID zoradené podľa času vzniku
            ownerId: ownerId,
            categoryId: categoryId,
            title: title.Trim(),
            description: description.Trim(),
            budgetAmount: budgetAmount,
            currency: "EUR",
            deadline: deadline,
            city: string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            status: ListingStatus.Open,
            createdAt: now,
            updatedAt: now);
    }

    /// <summary>Uzavrie inzerát: už neprijíma odpovede.</summary>
    public void Close(DateTimeOffset now)
    {
        if (Status == ListingStatus.Closed)
        {
            throw new InvalidOperationException("Inzerát je už uzavretý.");
        }

        Status = ListingStatus.Closed;
        UpdatedAt = now;
    }
}
