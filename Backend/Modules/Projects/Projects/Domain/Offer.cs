using Vybavim.Common.Errors;

namespace Vybavim.Modules.Projects.Domain;

/// <summary>
/// Ponuka odborníka na inzerát: za koľko, za ako dlho a krátka správa zadávateľovi.
/// Vlastnosti sa dajú meniť iba metódami tejto triedy (private set), rovnako ako pri inzeráte.
/// Porušené pravidlo = <see cref="DomainException"/>.
/// </summary>
internal sealed class Offer
{
    public const int MessageMaxLength = 2000;
    public const decimal MaxPrice = 1_000_000m;
    public const int MaxDeliveryDays = 365;

    // Private konštruktor: ponuka vznikne iba cez Create(). EF Core ho použije pri načítaní z DB.
    private Offer(
        Guid id,
        Guid listingId,
        Guid freelancerId,
        decimal price,
        int deliveryDays,
        string message,
        OfferStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        ListingId = listingId;
        FreelancerId = freelancerId;
        Price = price;
        DeliveryDays = deliveryDays;
        Message = message;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Inzerát z modulu Listings. Zámerne bez cudzieho kľúča – iná schéma, iný modul.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Kto ponuku poslal. ID používateľa z modulu Identity.</summary>
    public Guid FreelancerId { get; private set; }

    /// <summary>Navrhovaná cena v eurách.</summary>
    public decimal Price { get; private set; }

    /// <summary>Za koľko dní odborník prácu dodá.</summary>
    public int DeliveryDays { get; private set; }

    /// <summary>Správa zadávateľovi: prečo práve ja, ako by som postupoval.</summary>
    public string Message { get; private set; }

    public OfferStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Vytvorí novú čakajúcu ponuku. Jediný spôsob, ako ponuka vznikne.</summary>
    public static Offer Create(
        Guid listingId,
        Guid freelancerId,
        decimal price,
        int deliveryDays,
        string message,
        DateTimeOffset now)
    {
        // Cena: kladná, najviac milión, najviac na centy
        if (price is <= 0 or > MaxPrice || price != decimal.Round(price, 2))
        {
            throw new DomainException(OfferErrors.InvalidPrice);
        }

        if (deliveryDays is < 1 or > MaxDeliveryDays)
        {
            throw new DomainException(OfferErrors.InvalidDeliveryDays);
        }

        var cleanMessage = message?.Trim() ?? string.Empty;
        if (cleanMessage.Length == 0)
        {
            throw new DomainException(OfferErrors.MessageRequired);
        }

        if (cleanMessage.Length > MessageMaxLength)
        {
            throw new DomainException(OfferErrors.MessageTooLong);
        }

        return new Offer(
            id: Guid.CreateVersion7(now),
            listingId: listingId,
            freelancerId: freelancerId,
            price: price,
            deliveryDays: deliveryDays,
            message: cleanMessage,
            status: OfferStatus.Pending,
            createdAt: now,
            updatedAt: now);
    }

    /// <summary>Zadávateľ ponuku prijal.</summary>
    public void Accept(DateTimeOffset now) => Decide(OfferStatus.Accepted, now);

    /// <summary>Zadávateľ ponuku odmietol (alebo prijal inú).</summary>
    public void Reject(DateTimeOffset now) => Decide(OfferStatus.Rejected, now);

    /// <summary>Odborník ponuku stiahol.</summary>
    public void Withdraw(DateTimeOffset now) => Decide(OfferStatus.Withdrawn, now);

    /// <summary>Spoločné pravidlo: rozhodnúť sa dá iba o čakajúcej ponuke.</summary>
    private void Decide(OfferStatus next, DateTimeOffset now)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new DomainException(OfferErrors.InvalidStatusChange(Status, next));
        }

        Status = next;
        UpdatedAt = now;
    }
}
