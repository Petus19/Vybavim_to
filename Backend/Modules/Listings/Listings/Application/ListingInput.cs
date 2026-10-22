using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>
/// Údaje inzerátu, ktoré posiela web pri pridaní aj úprave. Tvar je rovnaký ako v odpovedi
/// detailu (budget, scope…), aby web mohol použiť ten istý formulár na pridanie aj úpravu.
/// </summary>
internal sealed record ListingInput(
    string Category,
    string Title,
    string Description,
    BudgetInput Budget,
    string? City,
    bool Remote,
    DateOnly? Deadline,
    ExperienceLevel Level,
    ScopeInput Scope,
    Engagement Engagement,
    bool InvoiceRequired,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string>? Deliverables,
    IReadOnlyList<string>? Requirements);

/// <summary>Rozpočet: { "type": "fixed", "amount": 300 } alebo { "type": "negotiable", "amount": null }.</summary>
internal sealed record BudgetInput(BudgetType Type, decimal? Amount);

/// <summary>Rozsah: { "size": "medium", "duration": "2–4 týždne" }.</summary>
internal sealed record ScopeInput(ProjectSize Size, string Duration);

/// <summary>Spoločné pomôcky pre pridanie a úpravu inzerátu.</summary>
internal static class ListingInputExtensions
{
    public static readonly Error UnknownCategory =
        Error.Validation("listing.unknown_category", "Takáto kategória neexistuje.");

    /// <summary>Web posiela kategóriu ako slug ("grafika"), inzerát potrebuje jej ID.</summary>
    public static Task<Guid?> FindCategoryIdAsync(this ListingsDbContext db, string slug, CancellationToken cancellationToken) =>
        db.Categories
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
