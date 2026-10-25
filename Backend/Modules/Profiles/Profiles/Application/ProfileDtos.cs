using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Application;

/// <summary>Údaje profilu, ktoré posiela web pri uložení (PUT /profiles/me).</summary>
internal sealed record ProfileInput(
    ProfileType Type,
    string DisplayName,
    string? City,
    string? Bio,
    IReadOnlyList<string>? Skills,
    string? Website);

/// <summary>Profil v odpovedi API.</summary>
internal sealed record ProfileResponse(
    Guid UserId,
    ProfileType Type,
    string DisplayName,
    string? City,
    string? Bio,
    IReadOnlyList<string> Skills,
    string? Website,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProfileResponse From(Profile p) =>
        new(p.UserId, p.Type, p.DisplayName, p.City, p.Bio, p.Skills, p.Website, p.CreatedAt, p.UpdatedAt);
}
