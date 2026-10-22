namespace Vybavim.Modules.Identity.Application;

// Tvary požiadaviek a odpovedí API modulu Identity.

/// <summary>Registrácia: e-mail a heslo. E-mail je zároveň používateľské meno.</summary>
internal sealed record RegisterRequest(string Email, string Password);

/// <summary>Prihlásenie: Login je e-mail alebo používateľské meno (napr. "Admin").</summary>
internal sealed record LoginRequest(string Login, string Password);

/// <summary>Obnovenie prístupu: refresh token z prihlásenia.</summary>
internal sealed record RefreshRequest(string RefreshToken);

/// <summary>Údaje o používateľovi – odpoveď na „kto som“ a na registráciu.</summary>
internal sealed record UserResponse(Guid Id, string UserName, string Email, IReadOnlyList<string> Roles, DateTimeOffset CreatedAt);
