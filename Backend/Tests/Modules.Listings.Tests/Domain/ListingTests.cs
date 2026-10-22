using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Tests.Domain;

/// <summary>Pravidlá inzerátu: Listing.Create(), Update() a zmeny stavu.</summary>
public sealed class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    /// <summary>
    /// Vytvorí platný inzerát. V teste zmeníme iba to, čo práve skúšame –
    /// ostatné parametre majú rozumné predvolené hodnoty.
    /// </summary>
    private static Listing CreateListing(
        string title = "Logo pre kaviareň",
        BudgetType budgetType = BudgetType.Fixed,
        decimal? budgetAmount = 300,
        string? city = "Bratislava",
        bool remote = false,
        DateOnly? deadline = null,
        IEnumerable<string>? skills = null) =>
        Listing.Create(
            ownerId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            title: title,
            description: "Popis práce",
            budgetType: budgetType,
            budgetAmount: budgetAmount,
            city: city,
            remote: remote,
            deadline: deadline,
            level: ExperienceLevel.Intermediate,
            size: ProjectSize.Small,
            estimatedDuration: "1 týždeň",
            engagement: Engagement.OneTime,
            invoiceRequired: false,
            skills: skills ?? ["Branding"],
            deliverables: null,
            requirements: null,
            now: Now);

    /// <summary>Upraví inzerát – opäť iba to, čo test skúša.</summary>
    private static void UpdateListing(Listing listing, string title = "Nový názov", decimal? budgetAmount = 500, DateTimeOffset? now = null) =>
        listing.Update(
            categoryId: listing.CategoryId,
            title: title,
            description: "Nový popis",
            budgetType: BudgetType.Fixed,
            budgetAmount: budgetAmount,
            city: "Košice",
            remote: true,
            deadline: null,
            level: ExperienceLevel.Expert,
            size: ProjectSize.Large,
            estimatedDuration: "2 mesiace",
            engagement: Engagement.Ongoing,
            invoiceRequired: true,
            skills: ["Ilustrácia"],
            deliverables: ["Výsledok"],
            requirements: null,
            now: now ?? Now.AddDays(1));

    /// <summary>Overí, že kód porušil pravidlo – hodil DomainException s očakávaným kódom chyby.</summary>
    private static void AssertBroken(string expectedCode, Action act)
    {
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(expectedCode, exception.Error.Code);
    }

    // --- Vytvorenie ---

    [Fact]
    public void Create_ValidInput_CreatesOpenListing()
    {
        var listing = CreateListing(title: "  Logo pre kaviareň  ");

        Assert.Equal(ListingStatus.Open, listing.Status);
        Assert.Equal("Logo pre kaviareň", listing.Title);   // medzery na okrajoch sú preč
        Assert.Equal("EUR", listing.Currency);
        Assert.Equal(Now, listing.CreatedAt);
        Assert.Equal(Now, listing.UpdatedAt);
        Assert.Empty(listing.Deliverables);                  // null → prázdny zoznam
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyTitle_Throws(string title)
    {
        AssertBroken("listing.title_required", () => CreateListing(title: title));
    }

    [Fact]
    public void Create_TitleTooLong_Throws()
    {
        var title = new string('a', Listing.TitleMaxLength + 1);

        AssertBroken("listing.title_too_long", () => CreateListing(title: title));
    }

    // --- Rozpočet ---

    [Fact]
    public void Create_NegotiableWithoutAmount_Succeeds()
    {
        var listing = CreateListing(budgetType: BudgetType.Negotiable, budgetAmount: null);

        Assert.Null(listing.BudgetAmount);
    }

    [Fact]
    public void Create_NegotiableWithAmount_Throws()
    {
        AssertBroken("listing.budget_amount_not_allowed",
            () => CreateListing(budgetType: BudgetType.Negotiable, budgetAmount: 100));
    }

    // Typ ceny je tu text, lebo enum BudgetType je internal a verejná testovacia metóda ho nemôže mať ako parameter.
    [Theory]
    [InlineData("Fixed", null)]
    [InlineData("Fixed", 0)]
    [InlineData("Hourly", -5)]
    public void Create_FixedOrHourlyWithoutPositiveAmount_Throws(string budgetType, int? amount)
    {
        var type = Enum.Parse<BudgetType>(budgetType);

        AssertBroken("listing.budget_amount_required", () => CreateListing(budgetType: type, budgetAmount: amount));
    }

    // --- Miesto a termín ---

    [Fact]
    public void Create_NotRemoteWithoutCity_Throws()
    {
        AssertBroken("listing.city_required", () => CreateListing(city: "  ", remote: false));
    }

    [Fact]
    public void Create_RemoteWithoutCity_Succeeds()
    {
        var listing = CreateListing(city: null, remote: true);

        Assert.Null(listing.City);
        Assert.True(listing.Remote);
    }

    [Fact]
    public void Create_DeadlineInPast_Throws()
    {
        AssertBroken("listing.deadline_in_past", () => CreateListing(deadline: Today.AddDays(-1)));
    }

    [Fact]
    public void Create_DeadlineToday_Succeeds()
    {
        var listing = CreateListing(deadline: Today);

        Assert.Equal(Today, listing.Deadline);
    }

    // --- Zručnosti ---

    [Fact]
    public void Create_WithoutSkills_Throws()
    {
        AssertBroken("listing.skills_required", () => CreateListing(skills: ["", "  "]));
    }

    [Fact]
    public void Create_Skills_AreTrimmedAndDeduplicated()
    {
        var listing = CreateListing(skills: [" Branding ", "branding", "Typografia", ""]);

        Assert.Equal(["Branding", "Typografia"], listing.Skills);
    }

    [Fact]
    public void Create_TooManySkills_Throws()
    {
        var skills = Enumerable.Range(1, Listing.ListMaxItems + 1).Select(i => $"Zručnosť {i}");

        AssertBroken("listing.too_many_items", () => CreateListing(skills: skills));
    }

    // --- Úprava ---

    [Fact]
    public void Update_OpenListing_ChangesFieldsAndUpdatedAt()
    {
        var listing = CreateListing();
        var later = Now.AddDays(3);

        UpdateListing(listing, title: "  Nový názov  ", now: later);

        Assert.Equal("Nový názov", listing.Title);
        Assert.Equal(500, listing.BudgetAmount);
        Assert.True(listing.Remote);
        Assert.Equal(["Ilustrácia"], listing.Skills);
        Assert.Equal(later, listing.UpdatedAt);
        Assert.Equal(Now, listing.CreatedAt);   // čas vytvorenia sa nemení
    }

    [Fact]
    public void Update_BreakingRule_ThrowsAndKeepsOriginalValues()
    {
        var listing = CreateListing(title: "Pôvodný názov");

        AssertBroken("listing.budget_amount_required", () => UpdateListing(listing, title: "Iný", budgetAmount: null));

        // Pravidlá sa overia ešte pred zmenou – inzerát ostal celý pôvodný
        Assert.Equal("Pôvodný názov", listing.Title);
        Assert.Equal(300, listing.BudgetAmount);
    }

    [Fact]
    public void Update_InProgressListing_Throws()
    {
        var listing = CreateListing();
        listing.Start(Now.AddDays(1));

        // Keď už odborník pracuje, zadanie sa nesmie meniť pod rukami
        AssertBroken("listing.not_editable", () => UpdateListing(listing));
    }

    // --- Zmeny stavu ---

    [Fact]
    public void Start_OpenListing_MovesToInProgress()
    {
        var listing = CreateListing();
        var later = Now.AddDays(2);

        listing.Start(later);

        Assert.Equal(ListingStatus.InProgress, listing.Status);
        Assert.Equal(later, listing.UpdatedAt);
    }

    [Fact]
    public void Complete_InProgressListing_MovesToCompleted()
    {
        var listing = CreateListing();
        listing.Start(Now.AddDays(1));

        listing.Complete(Now.AddDays(5));

        Assert.Equal(ListingStatus.Completed, listing.Status);
    }

    [Fact]
    public void Cancel_OpenListing_MovesToCancelled()
    {
        var listing = CreateListing();

        listing.Cancel(Now.AddDays(1));

        Assert.Equal(ListingStatus.Cancelled, listing.Status);
    }

    [Fact]
    public void Complete_OpenListing_Throws()
    {
        var listing = CreateListing();

        // Nedá sa dokončiť niečo, na čom sa ešte nezačalo pracovať
        AssertBroken("listing.invalid_status_change", () => listing.Complete(Now.AddDays(1)));
    }

    [Fact]
    public void Cancel_InProgressListing_Throws()
    {
        var listing = CreateListing();
        listing.Start(Now.AddDays(1));

        // Keď už odborník pracuje, inzerát sa nedá jednoducho stiahnuť
        AssertBroken("listing.invalid_status_change", () => listing.Cancel(Now.AddDays(2)));
    }
}
