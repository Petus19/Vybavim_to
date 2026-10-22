using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „autor upraví svoj inzerát“.</summary>
internal sealed class UpdateListing(
    ListingsDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    GetListingDetail getDetail)
{
    public static readonly Error NotOwner =
        Error.Forbidden("listing.not_owner", "Inzerát môže upraviť iba jeho autor.");

    public async Task<Result<ListingDetailResponse>> HandleAsync(Guid id, ListingInput input, CancellationToken cancellationToken)
    {
        // Bez AsNoTracking: EF si inzerát zapamätá a pri SaveChanges uloží jeho zmeny (UPDATE)
        var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<ListingDetailResponse>(GetListingDetail.NotFound);
        }

        if (listing.OwnerId != currentUser.Id)
        {
            return Result.Failure<ListingDetailResponse>(NotOwner);
        }

        var categoryId = await db.FindCategoryIdAsync(input.Category, cancellationToken);
        if (categoryId is null)
        {
            return Result.Failure<ListingDetailResponse>(ListingInputExtensions.UnknownCategory);
        }

        try
        {
            listing.Update(
                categoryId: categoryId.Value,
                title: input.Title,
                description: input.Description,
                budgetType: input.Budget.Type,
                budgetAmount: input.Budget.Amount,
                city: input.City,
                remote: input.Remote,
                deadline: input.Deadline,
                level: input.Level,
                size: input.Scope.Size,
                estimatedDuration: input.Scope.Duration,
                engagement: input.Engagement,
                invoiceRequired: input.InvoiceRequired,
                skills: input.Skills,
                deliverables: input.Deliverables,
                requirements: input.Requirements,
                now: timeProvider.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Result.Failure<ListingDetailResponse>(e.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await getDetail.HandleAsync(listing.Id, cancellationToken);
    }
}
