using Microsoft.EntityFrameworkCore;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Infrastructure;

namespace Vybavim.Modules.Profiles.Application;

/// <summary>
/// Implementácia kontraktu <see cref="IProfilesApi"/> pre ostatné moduly.
/// Je internal – ostatné moduly poznajú iba rozhranie z Profiles.Contracts, nie túto triedu ani tabuľky.
/// </summary>
internal sealed class ProfilesApi(ProfilesDbContext db) : IProfilesApi
{
    public async Task<IReadOnlyDictionary<Guid, ProfileSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, ProfileSummary>();
        }

        // Jeden dotaz pre všetky ID naraz: WHERE user_id = ANY(@ids)
        return await db.Profiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new ProfileSummary(p.UserId, p.DisplayName, p.Type, p.City))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);
    }
}
