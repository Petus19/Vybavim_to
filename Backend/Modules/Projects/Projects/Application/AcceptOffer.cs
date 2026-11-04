using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Projects.Domain;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>
/// Prípad použitia: „zadávateľ prijme ponuku“. Prvý biznis proces cez dva moduly:
/// ponuka → Accepted, ostatné čakajúce ponuky → Rejected (Projects) a inzerát → InProgress (Listings).
/// </summary>
internal sealed class AcceptOffer(
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

        // Prijať môže iba autor inzerátu (admin nie – rozhodnutie o odborníkovi je na zadávateľovi)
        var listing = (await listings.GetInfosAsync([offer.ListingId], cancellationToken)).GetValueOrDefault(offer.ListingId);
        if (listing is null || listing.OwnerId != currentUser.Id)
        {
            return Result.Failure<OfferResponse>(OfferErrors.NotListingOwner);
        }

        var now = timeProvider.GetUtcNow();
        var others = await db.Offers
            .Where(o => o.ListingId == offer.ListingId && o.Id != offer.Id && o.Status == OfferStatus.Pending)
            .ToListAsync(cancellationToken);

        try
        {
            offer.Accept(now);   // doména: prijať sa dá iba čakajúcu ponuku
            foreach (var other in others)
            {
                other.Reject(now);
            }
        }
        catch (DomainException e)
        {
            return Result.Failure<OfferResponse>(e.Error);
        }

        // Zmeny ponúk zapíšeme v transakcii a potvrdíme ich, až keď sa podarí prepnúť inzerát.
        // Ak inzerát medzitým nie je otvorený (stiahnutý, prijatá iná ponuka), transakcia sa zruší
        // a v databáze ostane všetko po starom. Súčasné prijatie dvoch ponúk zastavia zámky riadkov:
        // obe transakcie menia tie isté čakajúce ponuky, takže druhá počká a inzerát už nájde v InProgress.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var started = await listings.StartAsync(offer.ListingId, cancellationToken);
        if (started != StartListingResult.Started)
        {
            return Result.Failure<OfferResponse>(OfferErrors.ListingNotOpen);   // transakcia sa zruší pri await using
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(await responses.BuildAsync(offer, cancellationToken));
    }
}
