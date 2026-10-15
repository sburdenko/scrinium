using System.Security.Cryptography;
using System.Text;

namespace Scrinium.Server.Security;

public sealed class ApiKeyFilter(ServerWorkspaces workspaces) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var served = workspaces.Resolve(context.HttpContext);
        if (served is null)
            return WorkspaceResults.Unknown();

        if (string.IsNullOrEmpty(served.ApiKey))
            return TypedResults.Json(new ErrorDto("Admin endpoints are disabled: no API key is configured."), statusCode: 403);

        var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(served.ApiKey));

        return matches
            ? await next(context)
            : TypedResults.Json(new ErrorDto("Missing or wrong API key."), statusCode: 401);
    }
}
