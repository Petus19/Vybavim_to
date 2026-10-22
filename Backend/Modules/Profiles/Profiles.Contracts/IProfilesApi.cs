namespace Vybavim.Modules.Profiles.Contracts;

/// <summary>
/// Verejné rozhranie modulu Profiles pre OSTATNÉ moduly (volanie v rámci procesu, nie HTTP).
/// Napr. Listings ním zistí meno autora inzerátu bez toho, aby videl do tabuliek Profiles.
/// Implementácia je vnútri modulu Profiles a je internal.
/// </summary>
public interface IProfilesApi
{
    /// <summary>
    /// Krátke údaje o profiloch daných používateľov. Používatelia bez profilu vo výsledku nie sú.
    /// Berie viac ID naraz, aby sa dal zoznam inzerátov obslúžiť jedným dotazom.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, ProfileSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

/// <summary>Krátke údaje o profile – to, čo iné moduly potrebujú zobraziť (napr. karta zadávateľa).</summary>
public sealed record ProfileSummary(Guid UserId, string DisplayName, ProfileType Type, string? City);

/// <summary>Jednotlivec alebo firma.</summary>
public enum ProfileType
{
    Person,
    Company,
}
