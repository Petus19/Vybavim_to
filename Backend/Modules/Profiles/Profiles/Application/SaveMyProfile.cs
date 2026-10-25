using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Profiles.Domain;
using Vybavim.Modules.Profiles.Infrastructure;

namespace Vybavim.Modules.Profiles.Application;

/// <summary>
/// Prípad použitia: „ulož môj profil“. Ak profil ešte nemám, vytvorí sa; inak sa upraví.
/// (Tzv. upsert – update alebo insert.) Používateľ môže meniť iba SVOJ profil – ID berieme z tokenu.
/// </summary>
internal sealed class SaveMyProfile(ProfilesDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<ProfileResponse>> HandleAsync(ProfileInput input, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var now = timeProvider.GetUtcNow();

        // Bez AsNoTracking: pri existujúcom profile EF sleduje zmeny a uloží ich ako UPDATE
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        try
        {
            if (profile is null)
            {
                profile = Profile.Create(userId, input.Type, input.DisplayName, input.City, input.Bio, input.Skills, input.Website, now);
                db.Profiles.Add(profile);   // nový profil → INSERT
            }
            else
            {
                profile.Update(input.Type, input.DisplayName, input.City, input.Bio, input.Skills, input.Website, now);
            }
        }
        catch (DomainException e)
        {
            return Result.Failure<ProfileResponse>(e.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ProfileResponse.From(profile));
    }
}
