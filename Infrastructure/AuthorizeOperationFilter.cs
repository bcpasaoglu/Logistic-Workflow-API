using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LogisticDesk.Infrastructure
{
    /// <summary>
    /// Marks only the operations that really need a token with the Bearer scheme,
    /// so Swagger UI shows a lock on protected endpoints and not on login/register.
    /// </summary>
    public sealed class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
            if (!requiresAuth)
            {
                return;
            }

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>(),
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or invalid access token" });
        }
    }
}
