using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>
/// Prípad použitia: „stiahni inzerát“. Smie autor, alebo admin (moderovanie – napr. podvodný inzerát).
/// Inzerát sa nemaže, iba prejde do stavu Cancelled – ostane v histórii.
/// </summary>
internal sealed class CancelListing(
    ListingsDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    GetListingDetail getDetail)
{
    public static readonly Error NotAllowed =
        Error.Forbidden("listing.cancel_not_allowed", "Inzerát môže stiahnuť iba jeho autor alebo admin.");

    public async Task<Result<ListingDetailResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<ListingDetailResponse>(GetListingDetail.NotFound);
        }

        var isOwner = listing.OwnerId == currentUser.Id;
        if (!isOwner && !currentUser.IsInRole(Roles.Admin))
        {
            return Result.Failure<ListingDetailResponse>(NotAllowed);
        }

        try
        {
            listing.Cancel(timeProvider.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Result.Failure<ListingDetailResponse>(e.Error);   // napr. už prebieha → 409
        }

        await db.SaveChangesAsync(cancellationToken);
        return await getDetail.HandleAsync(listing.Id, cancellationToken);
    }
}
