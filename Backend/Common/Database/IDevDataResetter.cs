namespace Vybavim.Common.Database;

/// <summary>
/// Vráti demo dáta modulu do východiskového stavu (zmaže, čo pribudlo, a obnoví pôvodné).
/// Používa ho vyhodnotenie AI agenta, aby každý scenár začínal z rovnakého stavu.
/// Registruje sa IBA vo vývoji so zapnutým <see cref="DevData.DemoAccountsKey"/>;
/// volá ho endpoint POST /api/v1/dev/reset-demo-data, ktorý mimo vývoja neexistuje.
/// </summary>
public interface IDevDataResetter
{
    public Task ResetAsync(CancellationToken cancellationToken = default);
}
