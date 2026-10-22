using Microsoft.EntityFrameworkCore;
using Vybavim.Modules.Identity.Infrastructure;

namespace Vybavim.Modules.Identity.Application;

/// <summary>Prípad použitia pre admina: „zoznam všetkých používateľov s ich rolami“.</summary>
internal sealed class GetUsers(UsersDbContext db)
{
    public Task<List<UserResponse>> HandleAsync(CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserResponse(
                u.Id,
                u.UserName!,
                u.Email!,
                // Role používateľa: priradenia z user_roles spojené s názvami z roles
                db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .ToList(),
                u.CreatedAt))
            .ToListAsync(cancellationToken);
}
