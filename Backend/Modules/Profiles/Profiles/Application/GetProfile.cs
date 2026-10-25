using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Errors;
using Vybavim.Modules.Profiles.Infrastructure;

namespace Vybavim.Modules.Profiles.Application;

/// <summary>Prípad použitia: „zobraz profil používateľa“ – verejný profil aj môj profil.</summary>
internal sealed class GetProfile(ProfilesDbContext db)
{
    public static readonly Error NotFound =
        Error.NotFound("profile.not_found", "Profil neexistuje.");

    public async Task<Result<ProfileResponse>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile is null
            ? Result.Failure<ProfileResponse>(NotFound)
            : Result.Success(ProfileResponse.From(profile));
    }
}
