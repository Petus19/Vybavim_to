using Microsoft.AspNetCore.Identity;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Application;

/// <summary>Prípad použitia: „kto som“ – údaje o prihlásenom používateľovi.</summary>
internal sealed class GetCurrentUser(ICurrentUser currentUser, UserManager<User> userManager)
{
    public static readonly Error NotFound = Error.NotFound("identity.user_not_found", "Používateľ neexistuje.");

    public async Task<Result<UserResponse>> HandleAsync()
    {
        // Používateľ mohol byť medzitým zmazaný, hoci jeho token ešte platí
        var user = await userManager.FindByIdAsync(currentUser.Id.ToString());
        if (user is null)
        {
            return Result.Failure<UserResponse>(NotFound);
        }

        var roles = await userManager.GetRolesAsync(user);
        return Result.Success(new UserResponse(user.Id, user.UserName!, user.Email!, [.. roles], user.CreatedAt));
    }
}
