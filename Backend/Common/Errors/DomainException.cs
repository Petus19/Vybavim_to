namespace Vybavim.Common.Errors;

/// <summary>
/// Porušené biznis pravidlo v doméne (napr. „pri cene dohodou sa suma nezadáva“).
/// Nesie <see cref="Errors.Error"/> s kódom a slovenskou hláškou, takže Application vrstva
/// ju vie zmeniť na Result a endpoint na odpoveď 400 / 409 – bez toho, aby doména poznala HTTP.
/// </summary>
public sealed class DomainException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;
}
