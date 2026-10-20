using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Tests.Domain;

/// <summary>Pravidlá inzerátu z Listing.Create() a zmeny stavu.</summary>
public sealed class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

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
            deadline: null,
            level: ExperienceLevel.Intermediate,
            size: ProjectSize.Small,
            estimatedDuration: "1 týždeň",
            engagement: Engagement.OneTime,
            invoiceRequired: false,
            skills: skills ?? ["Branding"],
            deliverables: null,
            requirements: null,
            now: Now);

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
        Assert.Throws<ArgumentException>(() => CreateListing(title: title));
    }

    [Fact]
    public void Create_TitleTooLong_Throws()
    {
        var title = new string('a', Listing.TitleMaxLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateListing(title: title));
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
        Assert.Throws<ArgumentException>(() =>
            CreateListing(budgetType: BudgetType.Negotiable, budgetAmount: 100));
    }

    // Typ ceny je tu text, lebo enum BudgetType je internal a verejná testovacia metóda ho nemôže mať ako parameter.
    [Theory]
    [InlineData("Fixed", null)]
    [InlineData("Fixed", 0)]
    [InlineData("Hourly", -5)]
    public void Create_FixedOrHourlyWithoutPositiveAmount_Throws(string budgetType, int? amount)
    {
        var type = Enum.Parse<BudgetType>(budgetType);

        Assert.Throws<ArgumentException>(() => CreateListing(budgetType: type, budgetAmount: amount));
    }

    // --- Miesto ---

    [Fact]
    public void Create_NotRemoteWithoutCity_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateListing(city: "  ", remote: false));
    }

    [Fact]
    public void Create_RemoteWithoutCity_Succeeds()
    {
        var listing = CreateListing(city: null, remote: true);

        Assert.Null(listing.City);
        Assert.True(listing.Remote);
    }

    // --- Zručnosti ---

    [Fact]
    public void Create_WithoutSkills_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateListing(skills: ["", "  "]));
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

        Assert.Throws<ArgumentException>(() => CreateListing(skills: skills));
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
        Assert.Throws<InvalidOperationException>(() => listing.Complete(Now.AddDays(1)));
    }

    [Fact]
    public void Cancel_InProgressListing_Throws()
    {
        var listing = CreateListing();
        listing.Start(Now.AddDays(1));

        // Keď už odborník pracuje, inzerát sa nedá jednoducho stiahnuť
        Assert.Throws<InvalidOperationException>(() => listing.Cancel(Now.AddDays(2)));
    }
}
