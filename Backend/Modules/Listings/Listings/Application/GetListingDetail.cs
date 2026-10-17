using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „zobraz detail inzerátu“.</summary>
internal sealed class GetListingDetail(ListingsDbContext db)
{
    public static readonly Error NotFound = Error.NotFound("listing.not_found", "Inzerát neexistuje.");

    public async Task<Result<ListingDetailResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == id)
            // Join = spoj inzerát s jeho kategóriou, aby sme vedeli poslať slug ("grafika")
            .Join(db.Categories, l => l.CategoryId, c => c.Id, (l, c) => new ListingDetailResponse(
                l.Id,
                l.Title,
                l.Description,
                c.Slug,
                l.City,
                l.Remote,
                new BudgetResponse(l.BudgetType, l.BudgetAmount),
                l.Status,
                l.CreatedAt,
                l.Deadline,
                l.Level,
                new ScopeResponse(l.Size, l.EstimatedDuration),
                l.Engagement,
                l.InvoiceRequired,
                l.Skills,
                l.Deliverables,
                l.Requirements,
                l.OwnerId))
            .SingleOrDefaultAsync(cancellationToken);

        return listing is null
            ? Result.Failure<ListingDetailResponse>(NotFound)
            : Result.Success(listing);
    }
}
