using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Projects.Domain;
using Vybavim.Modules.Projects.Infrastructure;

namespace Vybavim.Modules.Projects.Application;

/// <summary>Prípad použitia: „odborník stiahne svoju ponuku“. Dá sa iba kým čaká na rozhodnutie.</summary>
internal sealed class WithdrawOffer(
    ProjectsDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    OfferResponseBuilder responses)
{
    public async Task<Result<OfferResponse>> HandleAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await db.Offers.SingleOrDefaultAsync(o => o.Id == offerId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferResponse>(OfferErrors.NotFound);
        }

        if (offer.FreelancerId != currentUser.Id)
        {
            return Result.Failure<OfferResponse>(OfferErrors.NotOfferAuthor);
        }

        try
        {
            offer.Withdraw(timeProvider.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Result.Failure<OfferResponse>(e.Error);   // už rozhodnutá → 409
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await responses.BuildAsync(offer, cancellationToken));
    }
}
