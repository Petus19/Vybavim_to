namespace Vybavim.Common.Errors;

/// <summary>Druh chyby – podľa neho sa vyberie HTTP stavový kód (400, 404, 409).</summary>
public enum ErrorType
{
    /// <summary>Zlé vstupné údaje → 400 Bad Request.</summary>
    Validation,

    /// <summary>Hľadaná vec neexistuje → 404 Not Found.</summary>
    NotFound,

    /// <summary>Operácia je v rozpore s aktuálnym stavom → 409 Conflict.</summary>
    Conflict,

    /// <summary>Nesprávne prihlasovacie údaje alebo neplatný token → 401 Unauthorized.</summary>
    Unauthorized,

    /// <summary>Používateľ je prihlásený, ale na túto vec nemá právo (napr. cudzí inzerát) → 403 Forbidden.</summary>
    Forbidden,
}

/// <summary>
/// Očakávaná chyba (nie pád programu): napr. „inzerát neexistuje“.
/// Code je stabilný strojový kód pre frontend ("listing.not_found"), Message je text pre ľudí.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
