using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace LogisticDesk.Controllers
{
    /// <summary>
    /// Base controller that turns plain-text business rule errors
    /// (<c>BadRequest("...")</c>, <c>NotFound("...")</c>, <c>Conflict("...")</c>)
    /// into RFC 7807 ProblemDetails, so every error body has the same shape.
    /// </summary>
    public abstract class ApiControllerBase : ControllerBase
    {
        public override BadRequestObjectResult BadRequest(object? error) => base.BadRequest(ToProblem(error, StatusCodes.Status400BadRequest));

        public override NotFoundObjectResult NotFound(object? value) => base.NotFound(ToProblem(value, StatusCodes.Status404NotFound));

        public override ConflictObjectResult Conflict(object? error) => base.Conflict(ToProblem(error, StatusCodes.Status409Conflict));

        private object? ToProblem(object? value, int status)
        {
            if (value is not string detail)
            {
                return value;
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = detail,
                Type = status switch
                {
                    StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    _ => null,
                },
            };

            var traceId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
            if (traceId != null)
            {
                problem.Extensions["traceId"] = traceId;
            }
            return problem;
        }
    }
}
