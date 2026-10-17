namespace Vybavim.Modules.Listings.Domain;

/// <summary>Ako zadávateľ zaplatí. Pevná cena a hodinová sadzba sa nikdy navzájom neporovnávajú.</summary>
internal enum BudgetType
{
    /// <summary>Pevná suma za celú prácu, napr. 300 €.</summary>
    Fixed,

    /// <summary>Sadzba za hodinu, napr. 25 €/hod.</summary>
    Hourly,

    /// <summary>Cena dohodou, suma nie je uvedená.</summary>
    Negotiable,
}
