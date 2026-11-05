using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Projects.Domain;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>Prípad použitia: „odborník pošle ponuku na inzerát“. Autorom ponuky je vždy ten, kto volá API.</summary>
internal sealed class SendOffer(
    ProjectsDbContext db,
    IListingsApi listings,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    OfferResponseBuilder responses)
{
    public async Task<Result<OfferResponse>> HandleAsync(Guid listingId, OfferInput input, CancellationToken cancellationToken)
    {
        var freelancerId = currentUser.Id;

        // 1. Inzerát sa opýtame modulu Listings cez jeho kontrakt – do jeho tabuliek nevidíme
        var listing = (await listings.GetInfosAsync([listingId], cancellationToken)).GetValueOrDefault(listingId);
        if (listing is null)
        {
            return Result.Failure<OfferResponse>(OfferErrors.ListingNotFound);
        }

        if (listing.OwnerId == freelancerId)
        {
            return Result.Failure<OfferResponse>(OfferErrors.OwnListing);
        }

        if (!listing.IsOpen)
        {
            return Result.Failure<OfferResponse>(OfferErrors.ListingNotOpen);
        }

        // 2. Jedna živá ponuka na inzerát. Po stiahnutí môže odborník poslať novú.
        var alreadySent = await db.Offers.AnyAsync(
            o => o.ListingId == listingId
                 && o.FreelancerId == freelancerId
                 && (o.Status == OfferStatus.Pending || o.Status == OfferStatus.Accepted),
            cancellationToken);
        if (alreadySent)
        {
            return Result.Failure<OfferResponse>(OfferErrors.AlreadySent);
        }

        // 3. Doména overí obsah ponuky (cena, termín, správa)
        Offer offer;
        try
        {
            offer = Offer.Create(listingId, freelancerId, input.Price, input.DeliveryDays, input.Message, timeProvider.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Result.Failure<OfferResponse>(e.Error);
        }

        db.Offers.Add(offer);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await responses.BuildAsync(offer, cancellationToken));
    }
}
