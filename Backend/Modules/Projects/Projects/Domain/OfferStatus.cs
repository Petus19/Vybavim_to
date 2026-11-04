namespace Vybavim.Modules.Projects.Domain;

/// <summary>
/// Životný cyklus ponuky: Pending → Accepted / Rejected / Withdrawn.
/// Rozhodnúť sa dá iba o čakajúcej ponuke – prijatá, odmietnutá ani stiahnutá sa už nemení.
/// </summary>
internal enum OfferStatus
{
    /// <summary>Odoslaná, čaká na rozhodnutie zadávateľa.</summary>
    Pending,

    /// <summary>Zadávateľ ju prijal – inzerát prešiel do stavu „pracuje sa“.</summary>
    Accepted,

    /// <summary>Zadávateľ ju odmietol, alebo prijal inú ponuku.</summary>
    Rejected,

    /// <summary>Odborník ju stiahol.</summary>
    Withdrawn,
}
