using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Vybavim.Common.Auth;
using Vybavim.Common.Errors;
using Vybavim.Modules.Identity.Application;

namespace Vybavim.Modules.Identity.Endpoints;

/// <summary>HTTP adresy modulu Identity: registrácia, prihlásenie, obnovenie tokenu, „kto som“, zoznam používateľov.</summary>
internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Prihlásenie");

        auth.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Registrácia nového používateľa (e-mail + heslo)");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Prihlásenie – vráti prístupový a refresh token")
            .Produces<AccessTokenResponse>();

        auth.MapPost("/google", LoginWithGoogleAsync)
            .WithName("LoginWithGoogle")
            .WithSummary("Prihlásenie cez Google – ID token od Google vymení za naše tokeny")
            .Produces<AccessTokenResponse>();

        auth.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .WithSummary("Nový prístupový token podľa refresh tokenu")
            .Produces<AccessTokenResponse>();

        auth.MapGet("/me", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("Údaje o prihlásenom používateľovi")
            .RequireAuthorization();                       // iba prihlásení

        app.MapGroup("/api/v1/users").WithTags("Správa používateľov")
            .MapGet("/", GetUsersAsync)
            .WithName("GetUsers")
            .WithSummary("Zoznam všetkých používateľov (iba admin)")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));   // iba admini

        return app;
    }

    private static async Task<Results<Created<UserResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        RegisterUser handler)
    {
        var result = await handler.HandleAsync(request);
        return result.IsSuccess
            ? TypedResults.Created("/api/v1/auth/me", result.Value)
            : result.Error!.ToProblem();
    }

    // SignIn s bearer schémou: ASP.NET z preukazu (ClaimsPrincipal) vytvorí tokeny a pošle ich ako JSON:
    // { "tokenType": "Bearer", "accessToken": "…", "expiresIn": 3600, "refreshToken": "…" }
    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        LoginUser handler)
    {
        var result = await handler.HandleAsync(request);
        return result.IsSuccess
            ? TypedResults.SignIn(result.Value, authenticationScheme: IdentityConstants.BearerScheme)
            : result.Error!.ToProblem();
    }

    // Rovnaké tokeny ako pri prihlásení heslom – web s nimi ďalej zaobchádza úplne rovnako
    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        LoginWithGoogle handler)
    {
        var result = await handler.HandleAsync(request);
        return result.IsSuccess
            ? TypedResults.SignIn(result.Value, authenticationScheme: IdentityConstants.BearerScheme)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> RefreshAsync(
        RefreshRequest request,
        RefreshAccess handler)
    {
        var result = await handler.HandleAsync(request);
        return result.IsSuccess
            ? TypedResults.SignIn(result.Value, authenticationScheme: IdentityConstants.BearerScheme)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> GetMeAsync(GetCurrentUser handler)
    {
        var result = await handler.HandleAsync();
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Ok<List<UserResponse>>> GetUsersAsync(GetUsers handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
