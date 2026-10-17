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
    public const int EstimatedDurationMaxLength = 60;

    /// <summary>Max. počet položiek v zoznamoch (zručnosti, výstupy, požiadavky).</summary>
    public const int ListMaxItems = 10;

    /// <summary>Max. dĺžka jednej položky zoznamu, napr. jednej zručnosti.</summary>
    public const int ListItemMaxLength = 100;

    // Konštruktor je private: zvonku sa inzerát dá vytvoriť iba cez Create(), ktorý overí pravidlá.
    // EF Core ho pri načítaní z databázy použije tiež (názvy parametrov = názvy vlastností).
    private Listing(
        Guid id,
        Guid ownerId,
        Guid categoryId,
        string title,
        string description,
        BudgetType budgetType,
        decimal? budgetAmount,
        string currency,
        string? city,
        bool remote,
        DateOnly? deadline,
        ExperienceLevel level,
        ProjectSize size,
        string estimatedDuration,
        Engagement engagement,
        bool invoiceRequired,
        string[] skills,
        string[] deliverables,
        string[] requirements,
        ListingStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OwnerId = ownerId;
        CategoryId = categoryId;
        Title = title;
        Description = description;
        BudgetType = budgetType;
        BudgetAmount = budgetAmount;
        Currency = currency;
        City = city;
        Remote = remote;
        Deadline = deadline;
        Level = level;
        Size = size;
        EstimatedDuration = estimatedDuration;
        Engagement = engagement;
        InvoiceRequired = invoiceRequired;
        Skills = skills;
        Deliverables = deliverables;
        Requirements = requirements;
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

    // --- Rozpočet ---

    public BudgetType BudgetType { get; private set; }

    /// <summary>Suma v eurách. Pri cene dohodou je null, inak je vždy väčšia ako 0.</summary>
    public decimal? BudgetAmount { get; private set; }

    /// <summary>Mena podľa ISO 4217, napr. "EUR".</summary>
    public string Currency { get; private set; }

    // --- Miesto a čas ---

    /// <summary>Mesto. Môže byť vyplnené aj pri práci na diaľku (zadávateľ je odtiaľ).</summary>
    public string? City { get; private set; }

    /// <summary>Dá sa pracovať na diaľku.</summary>
    public bool Remote { get; private set; }

    /// <summary>Termín dokončenia. Null = bez termínu.</summary>
    public DateOnly? Deadline { get; private set; }

    // --- Rozsah a podmienky ---

    public ExperienceLevel Level { get; private set; }
    public ProjectSize Size { get; private set; }

    /// <summary>Odhad trvania vlastnými slovami, napr. "2–4 týždne".</summary>
    public string EstimatedDuration { get; private set; }

    public Engagement Engagement { get; private set; }

    /// <summary>Zadávateľ potrebuje faktúru (odborník musí mať živnosť alebo firmu).</summary>
    public bool InvoiceRequired { get; private set; }

    // --- Zoznamy (v PostgreSQL ako pole textov text[]) ---

    /// <summary>Zručnosti ako štítky, napr. ["Branding", "Adobe Illustrator"]. Aspoň jedna.</summary>
    public string[] Skills { get; private set; }

    /// <summary>Čo má byť výsledkom. Môže byť prázdne.</summary>
    public string[] Deliverables { get; private set; }

    /// <summary>Požiadavky na odborníka (prax, portfólio, jazyk). Môže byť prázdne.</summary>
    public string[] Requirements { get; private set; }

    // --- Stav ---

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
        BudgetType budgetType,
        decimal? budgetAmount,
        string? city,
        bool remote,
        DateOnly? deadline,
        ExperienceLevel level,
        ProjectSize size,
        string estimatedDuration,
        Engagement engagement,
        bool invoiceRequired,
        IEnumerable<string> skills,
        IEnumerable<string>? deliverables,
        IEnumerable<string>? requirements,
        DateTimeOffset now)
    {
        // Texty
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(estimatedDuration);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(title.Trim().Length, TitleMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Trim().Length, DescriptionMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(estimatedDuration.Trim().Length, EstimatedDurationMaxLength);

        // Rozpočet: pri cene dohodou bez sumy, inak suma musí byť > 0
        if (budgetType == BudgetType.Negotiable && budgetAmount is not null)
        {
            throw new ArgumentException("Pri cene dohodou sa suma nezadáva.", nameof(budgetAmount));
        }

        if (budgetType != BudgetType.Negotiable && budgetAmount is not > 0)
        {
            throw new ArgumentException("Pri pevnej cene a hodinovej sadzbe musí byť suma väčšia ako 0.", nameof(budgetAmount));
        }

        // Miesto: buď na diaľku, alebo musí byť jasné, kde
        var cleanCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (!remote && cleanCity is null)
        {
            throw new ArgumentException("Ak práca nie je na diaľku, mesto je povinné.", nameof(city));
        }

        if (cleanCity is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cleanCity.Length, CityMaxLength);
        }

        // Zoznamy
        var cleanSkills = CleanList(skills, nameof(skills));
        if (cleanSkills.Length == 0)
        {
            throw new ArgumentException("Inzerát musí mať aspoň jednu zručnosť.", nameof(skills));
        }

        return new Listing(
            id: Guid.CreateVersion7(now),   // ID zoradené podľa času vzniku
            ownerId: ownerId,
            categoryId: categoryId,
            title: title.Trim(),
            description: description.Trim(),
            budgetType: budgetType,
            budgetAmount: budgetAmount,
            currency: "EUR",
            city: cleanCity,
            remote: remote,
            deadline: deadline,
            level: level,
            size: size,
            estimatedDuration: estimatedDuration.Trim(),
            engagement: engagement,
            invoiceRequired: invoiceRequired,
            skills: cleanSkills,
            deliverables: CleanList(deliverables ?? [], nameof(deliverables)),
            requirements: CleanList(requirements ?? [], nameof(requirements)),
            status: ListingStatus.Open,
            createdAt: now,
            updatedAt: now);
    }

    /// <summary>Zadávateľ si vybral odborníka, začína sa pracovať.</summary>
    public void Start(DateTimeOffset now) => ChangeStatus(ListingStatus.Open, ListingStatus.InProgress, now);

    /// <summary>Práca je hotová.</summary>
    public void Complete(DateTimeOffset now) => ChangeStatus(ListingStatus.InProgress, ListingStatus.Completed, now);

    /// <summary>Zadávateľ inzerát stiahol. Dá sa iba kým ešte nikto nepracuje.</summary>
    public void Cancel(DateTimeOffset now) => ChangeStatus(ListingStatus.Open, ListingStatus.Cancelled, now);

    /// <summary>Spoločné pravidlo pre všetky zmeny stavu: zmeniť sa dá iba z očakávaného stavu.</summary>
    private void ChangeStatus(ListingStatus expected, ListingStatus next, DateTimeOffset now)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Inzerát v stave {Status} sa nedá prepnúť na {next}.");
        }

        Status = next;
        UpdatedAt = now;
    }

    /// <summary>Orezaný zoznam bez prázdnych a duplicitných položiek, s kontrolou počtu a dĺžky.</summary>
    private static string[] CleanList(IEnumerable<string> items, string paramName)
    {
        var clean = items
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clean.Length > ListMaxItems)
        {
            throw new ArgumentException($"Najviac {ListMaxItems} položiek.", paramName);
        }

        if (clean.Any(item => item.Length > ListItemMaxLength))
        {
            throw new ArgumentException($"Položka môže mať najviac {ListItemMaxLength} znakov.", paramName);
        }

        return clean;
    }
}
