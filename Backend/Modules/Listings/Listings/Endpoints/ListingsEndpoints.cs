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
        var api = app.MapGroup("/api/v1").WithTags("Inzeráty");

        api.MapGet("/categories", GetCategoriesAsync)
            .WithName("GetCategories")
            .WithSummary("Zoznam hlavných kategórií");

        api.MapGet("/listings", SearchListingsAsync)
            .WithName("SearchListings")
            .WithSummary("Vyhľadávanie inzerátov s filtrami a zoradením");

        api.MapGet("/listings/{id:guid}", GetListingAsync)
            .WithName("GetListing")
            .WithSummary("Detail inzerátu");

        return app;
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
