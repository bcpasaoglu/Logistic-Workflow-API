using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticDesk.Infrastructure
{
    /// <summary>
    /// Turns unhandled exceptions into RFC 7807 ProblemDetails responses so clients
    /// always get a consistent JSON error body and stack traces never leak outside
    /// the Development environment.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(
            IProblemDetailsService problemDetails,
            ILogger<GlobalExceptionHandler> logger,
            IHostEnvironment environment)
        {
            _problemDetails = problemDetails;
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title) = Map(exception);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            }
            else
            {
                _logger.LogWarning(exception, "Request failed with {Status} for {Method} {Path}", status, httpContext.Request.Method, httpContext.Request.Path);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = httpContext.Request.Path,
                // Only expose exception details while developing locally.
                Detail = _environment.IsDevelopment() ? exception.Message : null,
            };

            httpContext.Response.StatusCode = status;
            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            });
        }

        /// <summary>Maps known exception types to an HTTP status code and a safe title.</summary>
        public static (int Status, string Title) Map(Exception exception) => exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was modified by another request. Reload it and try again."),
            DbUpdateException => (StatusCodes.Status409Conflict, "The change conflicts with the current state of the data."),
            BadHttpRequestException bad => (bad.StatusCode, "The request could not be read."),
            OperationCanceledException => (499, "The request was cancelled by the client."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };
    }
}
