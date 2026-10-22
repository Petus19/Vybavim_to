using Microsoft.AspNetCore.Identity;

namespace Vybavim.Modules.Identity.Domain;

/// <summary>
/// Používateľ platformy. Dedí z IdentityUser, ktorý už má všetko pre prihlásenie:
/// UserName, Email, PasswordHash (heslo nikdy neukladáme v čitateľnej podobe),
/// počítadlo neúspešných pokusov, zablokovanie a ďalšie.
/// Meno, profesia či logo firmy sem nepatria – to bude modul Profiles.
/// </summary>
internal sealed class User : IdentityUser<Guid>
{
    /// <summary>Kedy sa používateľ zaregistroval.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
