using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>
/// Implementácia kontraktu <see cref="IListingsApi"/> pre ostatné moduly.
/// Je internal – ostatné moduly poznajú iba rozhranie z Listings.Contracts, nie túto triedu ani tabuľky.
/// </summary>
internal sealed class ListingsApi(ListingsDbContext db, TimeProvider timeProvider) : IListingsApi
{
    public async Task<IReadOnlyDictionary<Guid, ListingInfo>> GetInfosAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return new Dictionary<Guid, ListingInfo>();
        }

        // Jeden dotaz pre všetky ID naraz: WHERE id = ANY(@ids)
        return await db.Listings
            .AsNoTracking()
            .Where(l => listingIds.Contains(l.Id))
            .Select(l => new ListingInfo(l.Id, l.OwnerId, l.Title, l.Status == ListingStatus.Open))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    public async Task<StartListingResult> StartAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);
        if (listing is null)
        {
            return StartListingResult.NotFound;
        }

        try
        {
            // Pravidlo „začať sa dá iba otvorený inzerát“ stráži doména – rovnako ako pri stiahnutí
            listing.Start(timeProvider.GetUtcNow());
        }
        catch (DomainException)
        {
            return StartListingResult.NotOpen;
        }

        await db.SaveChangesAsync(cancellationToken);
        return StartListingResult.Started;
    }
}
