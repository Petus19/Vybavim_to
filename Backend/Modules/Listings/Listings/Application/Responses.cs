using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Profiles.Contracts;

namespace Vybavim.Modules.Listings.Application;

// Tvary odpovedí API. Sú oddelené od domény: API posiela iba to, čo web potrebuje,
// a zmena vnútra Listing.cs nezmení automaticky to, čo vidí web.
// Enumy sa v JSON posielajú ako text v tvare snake_case: "in_progress", "one_time", "fixed".

/// <summary>Kategória v zozname kategórií.</summary>
internal sealed record CategoryResponse(string Slug, string Name, string Description);

/// <summary>Rozpočet. Pri type "negotiable" je Amount null.</summary>
internal sealed record BudgetResponse(BudgetType Type, decimal? Amount);

/// <summary>Veľkosť práce a odhad trvania vlastnými slovami.</summary>
internal sealed record ScopeResponse(ProjectSize Size, string Duration);

/// <summary>Inzerát v zozname – iba to, čo je na karte.</summary>
internal sealed record ListingSummaryResponse(
    Guid Id,
    string Title,
    string Category,
    string? City,
    bool Remote,
    BudgetResponse Budget,
    ListingStatus Status,
    ExperienceLevel Level,
    DateTimeOffset CreatedAt);

/// <summary>Celý inzerát pre stránku detailu.</summary>
internal sealed record ListingDetailResponse(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string? City,
    bool Remote,
    BudgetResponse Budget,
    ListingStatus Status,
    DateTimeOffset CreatedAt,
    DateOnly? Deadline,
    ExperienceLevel Level,
    ScopeResponse Scope,
    Engagement Engagement,
    bool InvoiceRequired,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Deliverables,
    IReadOnlyList<string> Requirements,
    Guid OwnerId,
    ListingOwnerResponse? Owner);

/// <summary>
/// Autor inzerátu pre kartu zadávateľa – údaje z modulu Profiles.
/// Null, ak autor ešte nemá vyplnený profil.
/// </summary>
internal sealed record ListingOwnerResponse(Guid Id, string DisplayName, ProfileType Type, string? City);

/// <summary>Jedna stránka výsledkov + celkový počet (pre „12 inzerátov“ a stránkovanie).</summary>
internal sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
