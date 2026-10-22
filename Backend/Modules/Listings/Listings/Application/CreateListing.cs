using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „prihlásený používateľ pridá inzerát“. Autorom je vždy ten, kto volá API.</summary>
internal sealed class CreateListing(
    ListingsDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    GetListingDetail getDetail)
{
    public async Task<Result<ListingDetailResponse>> HandleAsync(ListingInput input, CancellationToken cancellationToken)
    {
        // 1. Kategória musí existovať
        var categoryId = await db.FindCategoryIdAsync(input.Category, cancellationToken);
        if (categoryId is null)
        {
            return Result.Failure<ListingDetailResponse>(ListingInputExtensions.UnknownCategory);
        }

        // 2. Doména overí všetky pravidlá; porušené pravidlo → DomainException → chyba 400
        Listing listing;
        try
        {
            listing = Listing.Create(
                ownerId: currentUser.Id,   // autor = prihlásený používateľ, nie hodnota od klienta
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

        // 3. Uloženie – Add iba pripraví INSERT, SaveChangesAsync ho pošle do databázy
        db.Listings.Add(listing);
        await db.SaveChangesAsync(cancellationToken);

        // 4. Vrátime rovnaký detail ako GET /listings/{id}
        return await getDetail.HandleAsync(listing.Id, cancellationToken);
    }
}
