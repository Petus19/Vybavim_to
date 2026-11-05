namespace Vybavim.Modules.Listings.Contracts;

/// <summary>
/// Verejné rozhranie modulu Listings pre OSTATNÉ moduly (volanie v rámci procesu, nie HTTP).
/// Napr. Projects ním overí, či inzerát existuje a prijíma ponuky, a po prijatí ponuky ho prepne do stavu „pracuje sa“.
/// Implementácia je vnútri modulu Listings a je internal.
/// </summary>
public interface IListingsApi
{
    /// <summary>
    /// Základné údaje o inzerátoch (vrátane stiahnutých). Neexistujúce inzeráty vo výsledku nie sú.
    /// Berie viac ID naraz, aby sa dal zoznam ponúk obslúžiť jedným dotazom.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, ListingInfo>> GetInfosAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken);

    /// <summary>Zadávateľ si vybral odborníka: inzerát prejde z „otvorený“ do „pracuje sa“.</summary>
    public Task<StartListingResult> StartAsync(Guid listingId, CancellationToken cancellationToken);
}

/// <summary>Údaje o inzeráte, ktoré potrebujú iné moduly.</summary>
/// <param name="IsOpen">Inzerát je zverejnený a prijíma ponuky.</param>
public sealed record ListingInfo(Guid Id, Guid OwnerId, string Title, bool IsOpen);

/// <summary>Výsledok <see cref="IListingsApi.StartAsync"/>.</summary>
public enum StartListingResult
{
    Started,
    NotFound,

    /// <summary>Inzerát už nie je otvorený (pracuje sa, je hotový alebo stiahnutý).</summary>
    NotOpen,
}
