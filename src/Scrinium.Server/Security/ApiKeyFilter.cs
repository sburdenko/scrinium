using System.Security.Cryptography;
using System.Text;

namespace Scrinium.Server.Security;

public sealed class ApiKeyFilter(ServerOptions options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
            return TypedResults.Json(new ErrorDto("Admin endpoints are disabled: no API key is configured."), statusCode: 403);

        var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(options.ApiKey));

        return matches
            ? await next(context)
            : TypedResults.Json(new ErrorDto("Missing or wrong API key."), statusCode: 401);
    }
}
