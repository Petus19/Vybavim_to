using Vybavim.Common.Errors;

namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Inzerát: zadávateľ hľadá odborníka na prácu.
/// Vlastnosti sa dajú meniť iba metódami tejto triedy (private set),
/// takže biznis pravidlá nikto nemôže obísť. Porušené pravidlo = <see cref="DomainException"/>.
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

    /// <summary>Kto inzerát zadal. ID používateľa z modulu Identity.</summary>
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
    /// Vytvorí nový zverejnený inzerát. Jediný spôsob, ako inzerát vznikne.
    /// Pravidlá overí <see cref="Normalize"/> – rovnaké ako pri úprave.
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
        var f = Normalize(categoryId, title, description, budgetType, budgetAmount, city, remote, deadline,
            level, size, estimatedDuration, engagement, invoiceRequired, skills, deliverables, requirements, now);

        return new Listing(
            id: Guid.CreateVersion7(now),   // ID zoradené podľa času vzniku
            ownerId: ownerId,
            categoryId: f.CategoryId,
            title: f.Title,
            description: f.Description,
            budgetType: f.BudgetType,
            budgetAmount: f.BudgetAmount,
            currency: "EUR",
            city: f.City,
            remote: f.Remote,
            deadline: f.Deadline,
            level: f.Level,
            size: f.Size,
            estimatedDuration: f.EstimatedDuration,
            engagement: f.Engagement,
            invoiceRequired: f.InvoiceRequired,
            skills: f.Skills,
            deliverables: f.Deliverables,
            requirements: f.Requirements,
            status: ListingStatus.Open,
            createdAt: now,
            updatedAt: now);
    }

    /// <summary>
    /// Upraví inzerát. Dá sa iba kým je otvorený – keď už odborník pracuje,
    /// zadanie sa nesmie meniť pod rukami. Pravidlá sú rovnaké ako pri vytvorení.
    /// </summary>
    public void Update(
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
        if (Status != ListingStatus.Open)
        {
            throw new DomainException(ListingErrors.NotEditable);
        }

        var f = Normalize(categoryId, title, description, budgetType, budgetAmount, city, remote, deadline,
            level, size, estimatedDuration, engagement, invoiceRequired, skills, deliverables, requirements, now);

        CategoryId = f.CategoryId;
        Title = f.Title;
        Description = f.Description;
        BudgetType = f.BudgetType;
        BudgetAmount = f.BudgetAmount;
        City = f.City;
        Remote = f.Remote;
        Deadline = f.Deadline;
        Level = f.Level;
        Size = f.Size;
        EstimatedDuration = f.EstimatedDuration;
        Engagement = f.Engagement;
        InvoiceRequired = f.InvoiceRequired;
        Skills = f.Skills;
        Deliverables = f.Deliverables;
        Requirements = f.Requirements;
        UpdatedAt = now;
    }

    /// <summary>Zadávateľ si vybral odborníka, začína sa pracovať.</summary>
    public void Start(DateTimeOffset now) => ChangeStatus(ListingStatus.Open, ListingStatus.InProgress, now);

    /// <summary>Práca je hotová.</summary>
    public void Complete(DateTimeOffset now) => ChangeStatus(ListingStatus.InProgress, ListingStatus.Completed, now);

    /// <summary>Zadávateľ (alebo admin) inzerát stiahol. Dá sa iba kým ešte nikto nepracuje.</summary>
    public void Cancel(DateTimeOffset now) => ChangeStatus(ListingStatus.Open, ListingStatus.Cancelled, now);

    /// <summary>Spoločné pravidlo pre všetky zmeny stavu: zmeniť sa dá iba z očakávaného stavu.</summary>
    private void ChangeStatus(ListingStatus expected, ListingStatus next, DateTimeOffset now)
    {
        if (Status != expected)
        {
            throw new DomainException(ListingErrors.InvalidStatusChange(Status, next));
        }

        Status = next;
        UpdatedAt = now;
    }

    /// <summary>Upravené (orezané, skontrolované) hodnoty, ktoré sa dajú meniť pri vytvorení aj úprave.</summary>
    private readonly record struct EditableFields(
        Guid CategoryId,
        string Title,
        string Description,
        BudgetType BudgetType,
        decimal? BudgetAmount,
        string? City,
        bool Remote,
        DateOnly? Deadline,
        ExperienceLevel Level,
        ProjectSize Size,
        string EstimatedDuration,
        Engagement Engagement,
        bool InvoiceRequired,
        string[] Skills,
        string[] Deliverables,
        string[] Requirements);

    /// <summary>
    /// VŠETKY pravidlá pre obsah inzerátu na jednom mieste. Overí vstup, oreže texty
    /// a vráti upravené hodnoty. Pri porušení hodí <see cref="DomainException"/> s kódom a hláškou.
    /// </summary>
    private static EditableFields Normalize(
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
        // Texty: povinné a s maximálnou dĺžkou
        var cleanTitle = Required(title, TitleMaxLength, ListingErrors.TitleRequired, ListingErrors.TitleTooLong);
        var cleanDescription = Required(description, DescriptionMaxLength, ListingErrors.DescriptionRequired, ListingErrors.DescriptionTooLong);
        var cleanDuration = Required(estimatedDuration, EstimatedDurationMaxLength, ListingErrors.DurationRequired, ListingErrors.DurationTooLong);

        // Rozpočet: pri cene dohodou bez sumy, inak suma musí byť > 0
        if (budgetType == BudgetType.Negotiable && budgetAmount is not null)
        {
            throw new DomainException(ListingErrors.BudgetAmountNotAllowed);
        }

        if (budgetType != BudgetType.Negotiable && budgetAmount is not > 0)
        {
            throw new DomainException(ListingErrors.BudgetAmountRequired);
        }

        // Miesto: buď na diaľku, alebo musí byť jasné, kde
        var cleanCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (!remote && cleanCity is null)
        {
            throw new DomainException(ListingErrors.CityRequired);
        }

        if (cleanCity?.Length > CityMaxLength)
        {
            throw new DomainException(ListingErrors.CityTooLong);
        }

        // Termín: nie v minulosti (porovnávame iba dátumy, v UTC)
        if (deadline < DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new DomainException(ListingErrors.DeadlineInPast);
        }

        // Zoznamy
        var cleanSkills = CleanList(skills);
        if (cleanSkills.Length == 0)
        {
            throw new DomainException(ListingErrors.SkillsRequired);
        }

        return new EditableFields(
            categoryId,
            cleanTitle,
            cleanDescription,
            budgetType,
            budgetAmount,
            cleanCity,
            remote,
            deadline,
            level,
            size,
            cleanDuration,
            engagement,
            invoiceRequired,
            cleanSkills,
            CleanList(deliverables ?? []),
            CleanList(requirements ?? []));
    }

    /// <summary>Povinný text: orezaný, neprázdny a nie dlhší ako maxLength.</summary>
    private static string Required(string? value, int maxLength, Error whenEmpty, Error whenTooLong)
    {
        var clean = value?.Trim() ?? string.Empty;
        if (clean.Length == 0)
        {
            throw new DomainException(whenEmpty);
        }

        if (clean.Length > maxLength)
        {
            throw new DomainException(whenTooLong);
        }

        return clean;
    }

    /// <summary>Orezaný zoznam bez prázdnych a duplicitných položiek, s kontrolou počtu a dĺžky.</summary>
    private static string[] CleanList(IEnumerable<string> items)
    {
        var clean = items
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clean.Length > ListMaxItems)
        {
            throw new DomainException(ListingErrors.TooManyItems);
        }

        if (clean.Any(item => item.Length > ListItemMaxLength))
        {
            throw new DomainException(ListingErrors.ItemTooLong);
        }

        return clean;
    }
}
