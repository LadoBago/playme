using Microsoft.AspNetCore.Mvc;
using PlayMe.Application;
using PlayMe.Application.Errors;

namespace PlayMe.Api.Http;

/// <summary>
/// Turns an <see cref="AppResult{T}"/> into an <see cref="IActionResult"/>.
/// Success → 200 with the value; failure → <see cref="ProblemDetails"/>
/// carrying the i18n key (CLAUDE.md §3) under <c>code</c>.
/// </summary>
public static class AppResultActionExtensions
{
    public const string ProblemTypeBase = "https://playme.ge/errors/";

    public static ActionResult<T> ToActionResult<T>(this AppResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded)
        {
            return new OkObjectResult(result.Value);
        }
        return ToProblem(result.Error!.Value, result.Detail);
    }

    public static ActionResult ToProblem(ErrorCode code, string? detail)
    {
        var key = code.ToI18nKey(); // e.g. "errors.room.notFound"
        var status = code.ToHttpStatus();

        // Strip the "errors." prefix so the URI doesn't end up with a
        // duplicate "errors/errors/..." segment.
        var path = key.StartsWith("errors.", StringComparison.Ordinal)
            ? key["errors.".Length..]
            : key;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = key,
            Type = ProblemTypeBase + path.Replace('.', '/'),
            Detail = detail,
        };
        problem.Extensions["code"] = key;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
