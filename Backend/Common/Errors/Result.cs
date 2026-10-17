namespace Vybavim.Common.Errors;

/// <summary>
/// Výsledok operácie: buď hodnota (úspech), alebo <see cref="Error"/> (očakávaná chyba).
/// Namiesto vyhadzovania výnimiek pri bežných situáciách („nenašlo sa“) metóda vráti Result
/// a volajúci sa musí rozhodnúť, čo s chybou spraví.
/// Vytvára sa cez <see cref="Result.Success{T}"/> a <see cref="Result.Failure{T}"/>.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    /// <summary>Hodnota pri úspechu. Pri chybe vyhodí výnimku – vždy najprv skontrolovať IsSuccess.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Výsledok je chyba, nemá hodnotu.");
}

/// <summary>Vytváranie výsledkov: Result.Success(hodnota) alebo Result.Failure&lt;T&gt;(chyba).</summary>
public static class Result
{
    /// <summary>Úspech s hodnotou. Typ T si C# odvodí sám z hodnoty.</summary>
    public static Result<T> Success<T>(T value) => new(value, null);

    /// <summary>Chyba. Typ T treba uviesť, lebo z chyby sa odvodiť nedá.</summary>
    public static Result<T> Failure<T>(Error error) => new(default, error);
}
