namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Životný cyklus inzerátu: Open → InProgress → Completed, alebo Open → Cancelled.
/// Samotnú prácu (dohoda, chat, odovzdanie) riadi modul Projects;
/// inzerát si podľa jeho udalostí iba prepne stav.
/// </summary>
internal enum ListingStatus
{
    /// <summary>Zverejnený, prijíma odpovede.</summary>
    Open,

    /// <summary>Zadávateľ si vybral odborníka a pracuje sa.</summary>
    InProgress,

    /// <summary>Práca je hotová.</summary>
    Completed,

    /// <summary>Zadávateľ inzerát stiahol.</summary>
    Cancelled,
}
