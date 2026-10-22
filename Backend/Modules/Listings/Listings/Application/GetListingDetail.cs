using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „zobraz detail inzerátu“.</summary>
internal sealed class GetListingDetail(ListingsDbContext db, ICurrentUser currentUser)
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

        if (listing is null || (listing.Status == ListingStatus.Cancelled && !CanSeeCancelled(listing.OwnerId)))
        {
            return Result.Failure<ListingDetailResponse>(NotFound);
        }

        return Result.Success(listing);
    }

    /// <summary>
    /// Stiahnutý inzerát vidí iba jeho autor a admin. Pre ostatných „neexistuje“ (404) –
    /// nemusia vedieť, že tu niečo bolo.
    /// </summary>
    private bool CanSeeCancelled(Guid ownerId) =>
        currentUser.IsAuthenticated && (currentUser.Id == ownerId || currentUser.IsInRole(Roles.Admin));
}
