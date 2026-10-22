using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „moje inzeráty“ – všetky stavy vrátane stiahnutých, najnovšie prvé.</summary>
internal sealed class GetMyListings(ListingsDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResponse<ListingSummaryResponse>>> HandleAsync(
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var pageNumber = page ?? 1;
        var size = pageSize ?? SearchListings.DefaultPageSize;
        if (pageNumber < 1 || size is < 1 or > SearchListings.MaxPageSize)
        {
            return Result.Failure<PagedResponse<ListingSummaryResponse>>(Error.Validation(
                "listing.invalid_page", $"Stránka musí byť aspoň 1 a veľkosť stránky 1 až {SearchListings.MaxPageSize}."));
        }

        var ownerId = currentUser.Id;
        var mine = db.Listings
            .AsNoTracking()
            .Where(l => l.OwnerId == ownerId)   // index ix_listings_owner_id
            .Join(db.Categories, l => l.CategoryId, c => c.Id, (l, c) => new { Listing = l, CategorySlug = c.Slug });

        var total = await mine.CountAsync(cancellationToken);
        var items = await mine
            .OrderByDescending(x => x.Listing.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(x => new ListingSummaryResponse(
                x.Listing.Id,
                x.Listing.Title,
                x.CategorySlug,
                x.Listing.City,
                x.Listing.Remote,
                new BudgetResponse(x.Listing.BudgetType, x.Listing.BudgetAmount),
                x.Listing.Status,
                x.Listing.Level,
                x.Listing.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResponse<ListingSummaryResponse>(items, total, pageNumber, size));
    }
}
