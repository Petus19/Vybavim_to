using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Vybavim.Common.Errors;

/// <summary>Prevod <see cref="Error"/> na HTTP odpoveď vo formáte ProblemDetails (RFC 9457).</summary>
public static class ErrorResults
{
    /// <summary>
    /// Vráti odpoveď, napr. pre NotFound:
    /// { "status": 404, "title": "Inzerát neexistuje.", "code": "listing.not_found" }
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
