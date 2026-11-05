using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Vybavim.Common.Errors;
using Vybavim.Modules.Projects.Application;

namespace Vybavim.Modules.Projects.Endpoints;

/// <summary>
/// HTTP adresy ponúk. Všetky sú iba pre prihlásených. Endpoint iba prevezme požiadavku,
/// zavolá prípad použitia a výsledok prevedie na HTTP odpoveď.
/// </summary>
internal static class OffersEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Offers").RequireAuthorization();

        api.MapPost("/listings/{listingId:guid}/offers", SendOfferAsync)
            .WithName("SendOffer")
            .WithSummary("Send an offer on a listing – the signed-in user becomes its author");

        api.MapGet("/listings/{listingId:guid}/offers", GetListingOffersAsync)
            .WithName("GetListingOffers")
            .WithSummary("Offers on a listing – listing author or admin only");

        api.MapGet("/offers/mine", GetMyOffersAsync)
            .WithName("GetMyOffers")
            .WithSummary("My sent offers – all statuses, newest first");

        api.MapPost("/offers/{id:guid}/accept", AcceptOfferAsync)
            .WithName("AcceptOffer")
            .WithSummary("Accept an offer – listing author only; other pending offers are rejected and the listing moves to in_progress");

        api.MapPost("/offers/{id:guid}/reject", RejectOfferAsync)
            .WithName("RejectOffer")
            .WithSummary("Reject an offer – listing author only");

        api.MapPost("/offers/{id:guid}/withdraw", WithdrawOfferAsync)
            .WithName("WithdrawOffer")
            .WithSummary("Withdraw my offer – while it is pending");

        return app;
    }

    private static async Task<Results<Created<OfferResponse>, ProblemHttpResult>> SendOfferAsync(
        Guid listingId,
        OfferInput input,
        SendOffer handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(listingId, input, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/listings/{listingId}/offers", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<List<OfferResponse>>, ProblemHttpResult>> GetListingOffersAsync(
        Guid listingId,
        GetListingOffers handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(listingId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Ok<List<OfferResponse>>> GetMyOffersAsync(
        GetMyOffers handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Results<Ok<OfferResponse>, ProblemHttpResult>> AcceptOfferAsync(
        Guid id,
        AcceptOffer handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<OfferResponse>, ProblemHttpResult>> RejectOfferAsync(
        Guid id,
        RejectOffer handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<OfferResponse>, ProblemHttpResult>> WithdrawOfferAsync(
        Guid id,
        WithdrawOffer handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
