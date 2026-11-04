using Vybavim.Modules.Listings.Contracts;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Projects.Domain;

namespace Vybavim.Modules.Projects.Application;

/// <summary>Ponuka, ktorú posiela odborník: { "price": 280, "deliveryDays": 14, "message": "…" }.</summary>
internal sealed record OfferInput(decimal Price, int DeliveryDays, string Message);

/// <summary>
/// Ponuka v odpovedi API. Obsahuje aj názov inzerátu (pre „moje ponuky“) a kartu odborníka
/// (pre zadávateľa, ktorý ponuky porovnáva) – oboje z iných modulov cez ich kontrakty.
/// </summary>
internal sealed record OfferResponse(
    Guid Id,
    Guid ListingId,
    string? ListingTitle,
    Guid FreelancerId,
    FreelancerResponse? Freelancer,
    decimal Price,
    int DeliveryDays,
    string Message,
    OfferStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Karta odborníka z modulu Profiles. Null, kým si odborník nevyplní profil.</summary>
internal sealed record FreelancerResponse(Guid Id, string DisplayName, ProfileType Type, string? City);

/// <summary>
/// Poskladá odpovede pre ľubovoľný zoznam ponúk: názvy inzerátov a profily odborníkov
/// dotiahne z iných modulov naraz (2 dotazy pre celý zoznam, nie 2 na každú ponuku).
/// </summary>
internal sealed class OfferResponseBuilder(IListingsApi listings, IProfilesApi profiles)
{
    public async Task<List<OfferResponse>> BuildAsync(IReadOnlyCollection<Offer> offers, CancellationToken cancellationToken)
    {
        var listingInfos = await listings.GetInfosAsync(offers.Select(o => o.ListingId).Distinct().ToList(), cancellationToken);
        var freelancers = await profiles.GetSummariesAsync(offers.Select(o => o.FreelancerId).Distinct().ToList(), cancellationToken);

        return offers
            .Select(o => new OfferResponse(
                o.Id,
                o.ListingId,
                listingInfos.GetValueOrDefault(o.ListingId)?.Title,
                o.FreelancerId,
                freelancers.TryGetValue(o.FreelancerId, out var p) ? new FreelancerResponse(p.UserId, p.DisplayName, p.Type, p.City) : null,
                o.Price,
                o.DeliveryDays,
                o.Message,
                o.Status,
                o.CreatedAt,
                o.UpdatedAt))
            .ToList();
    }

    public async Task<OfferResponse> BuildAsync(Offer offer, CancellationToken cancellationToken) =>
        (await BuildAsync([offer], cancellationToken))[0];
}
