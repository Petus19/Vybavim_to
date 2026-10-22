using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Vybavim.Common.Auth;

/// <summary>
/// „Kto práve volá API.“ Moduly si ho vypýtajú cez Dependency Injection, napr. pri
/// pridaní inzerátu: ownerId = currentUser.Id. Nevedia nič o tokenoch ani o Identity.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Je požiadavka od prihláseného používateľa?</summary>
    public bool IsAuthenticated { get; }

    /// <summary>ID prihláseného používateľa. Pri neprihlásenom vyhodí výnimku – najprv skontrolovať IsAuthenticated.</summary>
    public Guid Id { get; }

    /// <summary>Má používateľ danú rolu (napr. <see cref="Roles.Admin"/>)?</summary>
    public bool IsInRole(string role);
}

/// <summary>Číta údaje o používateľovi z aktuálnej HTTP požiadavky (z tokenu, ktorý overilo ASP.NET).</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Používateľ nie je prihlásený.");

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
