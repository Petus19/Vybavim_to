using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Application;

namespace Vybavim.Modules.Listings.Endpoints;

/// <summary>
/// HTTP adresy modulu Listings. Endpoint iba prevezme požiadavku, zavolá prípad použitia
/// z Application a výsledok prevedie na HTTP odpoveď – žiadna logika navyše.
/// </summary>
internal static class ListingsEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Listings");

        api.MapGet("/categories", GetCategoriesAsync)
            .WithName("GetCategories")
            .WithSummary("List main categories");

        api.MapGet("/listings", SearchListingsAsync)
            .WithName("SearchListings")
            .WithSummary("Search listings with filters, sorting and paging");

        api.MapGet("/listings/{id:guid}", GetListingAsync)
            .WithName("GetListing")
            .WithSummary("Listing detail, including the author's profile");

        // --- Správa inzerátov: iba pre prihlásených ---

        api.MapPost("/listings", CreateListingAsync)
            .WithName("CreateListing")
            .WithSummary("Create a listing – the signed-in user becomes its author")
            .RequireAuthorization();

        api.MapPut("/listings/{id:guid}", UpdateListingAsync)
            .WithName("UpdateListing")
            .WithSummary("Update a listing – author only, while the listing is open")
            .RequireAuthorization();

        api.MapPost("/listings/{id:guid}/cancel", CancelListingAsync)
            .WithName("CancelListing")
            .WithSummary("Withdraw a listing – author or admin")
            .RequireAuthorization();

        api.MapGet("/listings/mine", GetMyListingsAsync)
            .WithName("GetMyListings")
            .WithSummary("My listings – all statuses, newest first")
            .RequireAuthorization();

        return app;
    }

    private static async Task<Results<Created<ListingDetailResponse>, ProblemHttpResult>> CreateListingAsync(
        ListingInput input,
        CreateListing handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(input, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/listings/{result.Value.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ListingDetailResponse>, ProblemHttpResult>> UpdateListingAsync(
        Guid id,
        ListingInput input,
        UpdateListing handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, input, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ListingDetailResponse>, ProblemHttpResult>> CancelListingAsync(
        Guid id,
        CancelListing handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PagedResponse<ListingSummaryResponse>>, ProblemHttpResult>> GetMyListingsAsync(
        int? page,
        int? pageSize,
        GetMyListings handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(page, pageSize, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Ok<List<CategoryResponse>>> GetCategoriesAsync(
        GetCategories handler,
        CancellationToken cancellationToken)
    {
        var categories = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok(categories);
    }

    private static async Task<Results<Ok<PagedResponse<ListingSummaryResponse>>, ProblemHttpResult>> SearchListingsAsync(
        [AsParameters] ListingSearchQuery query,
        SearchListings handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ListingDetailResponse>, ProblemHttpResult>> GetListingAsync(
        Guid id,
        GetListingDetail handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
