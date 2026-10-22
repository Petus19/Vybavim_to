using Microsoft.AspNetCore.Identity;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Domain;

namespace Vybavim.Modules.Identity.Application;

/// <summary>Prípad použitia: „zaregistruj nového používateľa“.</summary>
internal sealed class RegisterUser(UserManager<User> userManager, TimeProvider timeProvider)
{
    public async Task<Result<UserResponse>> HandleAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<UserResponse>(
                Error.Validation("identity.missing_fields", "E-mail aj heslo sú povinné."));
        }

        var now = timeProvider.GetUtcNow();
        var email = request.Email.Trim();
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            UserName = email,   // bežní používatelia sa prihlasujú e-mailom
            Email = email,
            CreatedAt = now,
        };

        // UserManager overí pravidlá (tvar e-mailu, jedinečnosť, sila hesla), heslo zahashuje a uloží.
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join(" ", result.Errors.Select(e => e.Description));
            return Result.Failure<UserResponse>(Error.Validation("identity.registration_failed", message));
        }

        return Result.Success(new UserResponse(user.Id, user.UserName, user.Email, [], user.CreatedAt));
    }
}
