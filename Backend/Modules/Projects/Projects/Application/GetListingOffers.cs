using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Projects.Domain;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>
/// Prípad použitia: „ponuky na môj inzerát“. Vidí ich iba autor inzerátu (a admin) –
/// ceny ostatných odborníkov nie sú verejné.
/// </summary>
internal sealed class GetListingOffers(
    ProjectsDbContext db,
    IListingsApi listings,
    ICurrentUser currentUser,
    OfferResponseBuilder responses)
{
    public async Task<Result<List<OfferResponse>>> HandleAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = (await listings.GetInfosAsync([listingId], cancellationToken)).GetValueOrDefault(listingId);
        if (listing is null)
        {
            return Result.Failure<List<OfferResponse>>(OfferErrors.ListingNotFound);
        }

        if (listing.OwnerId != currentUser.Id && !currentUser.IsInRole(Roles.Admin))
        {
            return Result.Failure<List<OfferResponse>>(OfferErrors.NotListingOwner);
        }

        // Čakajúce ponuky hore (o tých sa rozhoduje), v rámci skupiny najstaršie prvé
        var offers = await db.Offers
            .AsNoTracking()
            .Where(o => o.ListingId == listingId)
            .OrderBy(o => o.Status != OfferStatus.Pending)
            .ThenBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(await responses.BuildAsync(offers, cancellationToken));
    }
}
