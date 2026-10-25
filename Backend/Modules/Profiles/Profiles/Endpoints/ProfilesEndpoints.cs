using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Profiles.Application;

namespace Vybavim.Modules.Profiles.Endpoints;

/// <summary>HTTP adresy modulu Profiles: môj profil (zobraziť, uložiť) a verejný profil.</summary>
internal static class ProfilesEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var profiles = app.MapGroup("/api/v1/profiles").WithTags("Profiles");

        profiles.MapGet("/me", GetMyProfileAsync)
            .WithName("GetMyProfile")
            .WithSummary("My profile (404 until it is created)")
            .RequireAuthorization();

        profiles.MapPut("/me", SaveMyProfileAsync)
            .WithName("SaveMyProfile")
            .WithSummary("Create or update my profile")
            .RequireAuthorization();

        profiles.MapGet("/{userId:guid}", GetProfileAsync)
            .WithName("GetProfile")
            .WithSummary("Public user profile");

        return app;
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> GetMyProfileAsync(
        GetProfile handler,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(currentUser.Id, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> SaveMyProfileAsync(
        ProfileInput input,
        SaveMyProfile handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(input, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> GetProfileAsync(
        Guid userId,
        GetProfile handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(userId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
