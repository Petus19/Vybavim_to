using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>Prípad použitia: „moje odoslané ponuky“ – všetky stavy, najnovšie prvé, s názvom inzerátu.</summary>
internal sealed class GetMyOffers(ProjectsDbContext db, ICurrentUser currentUser, OfferResponseBuilder responses)
{
    public async Task<List<OfferResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var freelancerId = currentUser.Id;
        var offers = await db.Offers
            .AsNoTracking()
            .Where(o => o.FreelancerId == freelancerId)   // index ix_offers_freelancer_id
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return await responses.BuildAsync(offers, cancellationToken);
    }
}
