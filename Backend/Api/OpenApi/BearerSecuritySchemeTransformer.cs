using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Vybavim.Api.OpenApi;

/// <summary>
/// Doplní do popisu API (OpenAPI), že API používa prihlásenie tokenom v hlavičke
/// "Authorization: Bearer …". Vďaka tomu sa dá v Scalar token zadať a skúšať chránené endpointy.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Prístupový token z POST /api/v1/auth/login (pole accessToken).",
        };

        return Task.CompletedTask;
    }
}
