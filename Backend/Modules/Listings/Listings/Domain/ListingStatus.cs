namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Stav inzerátu. Priebeh samotnej práce (prebieha / dokončený) eviduje modul Projects,
/// inzerát iba hovorí, či ešte prijíma odpovede.
/// </summary>
internal enum ListingStatus
{
    /// <summary>Zverejnený, prijíma odpovede.</summary>
    Open,

    /// <summary>Uzavretý: zadávateľ si vybral odborníka alebo inzerát stiahol.</summary>
    Closed,
}
