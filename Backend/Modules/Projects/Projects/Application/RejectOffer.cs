using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Projects.Domain;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>Prípad použitia: „zadávateľ odmietne ponuku“. Smie iba autor inzerátu.</summary>
internal sealed class RejectOffer(
    ProjectsDbContext db,
    IListingsApi listings,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    OfferResponseBuilder responses)
{
    public async Task<Result<OfferResponse>> HandleAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await db.Offers.SingleOrDefaultAsync(o => o.Id == offerId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferResponse>(OfferErrors.NotFound);
        }

        var listing = (await listings.GetInfosAsync([offer.ListingId], cancellationToken)).GetValueOrDefault(offer.ListingId);
        if (listing is null || listing.OwnerId != currentUser.Id)
        {
            return Result.Failure<OfferResponse>(OfferErrors.NotListingOwner);
        }

        try
        {
            offer.Reject(timeProvider.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Result.Failure<OfferResponse>(e.Error);   // už rozhodnutá → 409
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await responses.BuildAsync(offer, cancellationToken));
    }
}
