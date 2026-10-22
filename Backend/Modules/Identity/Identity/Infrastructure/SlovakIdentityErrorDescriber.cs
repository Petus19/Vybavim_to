using Microsoft.AspNetCore.Identity;

namespace Vybavim.Modules.Identity.Infrastructure;

/// <summary>
/// Chybové hlášky ASP.NET Core Identity po slovensky (predvolene sú anglicky).
/// Kód chyby (Code) ostáva rovnaký, mení sa iba text pre ľudí.
/// </summary>
internal sealed class SlovakIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Nastala neznáma chyba." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"E-mail {email} je už zaregistrovaný." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Používateľské meno {userName} už existuje." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "E-mail nemá platný tvar." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Používateľské meno obsahuje nepovolené znaky." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Heslo musí mať aspoň {length} znakov." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Heslo musí obsahovať aspoň jednu číslicu." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Heslo musí obsahovať aspoň jedno malé písmeno." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Heslo musí obsahovať aspoň jedno veľké písmeno." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Heslo musí obsahovať aspoň jeden špeciálny znak." };
}
