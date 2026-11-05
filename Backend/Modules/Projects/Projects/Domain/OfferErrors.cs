using Vybavim.Common.Errors;

namespace Vybavim.Modules.Projects.Domain;

/// <summary>Porušenia pravidiel ponuky: stabilný kód pre web a slovenská hláška pre ľudí.</summary>
internal static class OfferErrors
{
    // --- Obsah ponuky (400) ---

    public static readonly Error InvalidPrice =
        Error.Validation("offer.invalid_price", "Cena musí byť väčšia ako 0 € a najviac 1 000 000 € (najviac na centy).");

    public static readonly Error InvalidDeliveryDays =
        Error.Validation("offer.invalid_delivery_days", $"Termín dodania musí byť 1 až {Offer.MaxDeliveryDays} dní.");

    public static readonly Error MessageRequired =
        Error.Validation("offer.message_required", "Správa pre zadávateľa je povinná.");

    public static readonly Error MessageTooLong =
        Error.Validation("offer.message_too_long", $"Správa môže mať najviac {Offer.MessageMaxLength} znakov.");

    // --- Kto smie a kedy (403, 404, 409) ---

    public static readonly Error NotFound =
        Error.NotFound("offer.not_found", "Ponuka neexistuje.");

    public static readonly Error ListingNotFound =
        Error.NotFound("listing.not_found", "Inzerát neexistuje.");

    public static readonly Error OwnListing =
        Error.Forbidden("offer.own_listing", "Na vlastný inzerát nemôžete poslať ponuku.");

    public static readonly Error NotListingOwner =
        Error.Forbidden("offer.not_listing_owner", "Toto môže urobiť iba autor inzerátu.");

    public static readonly Error NotOfferAuthor =
        Error.Forbidden("offer.not_author", "Ponuku môže stiahnuť iba ten, kto ju poslal.");

    public static readonly Error ListingNotOpen =
        Error.Conflict("offer.listing_not_open", "Inzerát už neprijíma ponuky.");

    public static readonly Error AlreadySent =
        Error.Conflict("offer.already_sent", "Na tento inzerát už máte poslanú ponuku.");

    public static Error InvalidStatusChange(OfferStatus from, OfferStatus to) =>
        Error.Conflict("offer.invalid_status_change", $"Ponuku v stave {from} sa nedá zmeniť na {to}.");
}
