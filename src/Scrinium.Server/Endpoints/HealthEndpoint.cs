using Scrinium.Core.Documents;
using Scrinium.Ollama;

namespace Scrinium.Server.Endpoints;

internal static class HealthEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes.MapGet("/health", HandleAsync);

    private static async Task<IResult> HandleAsync(
        ServerWorkspaces workspaces,
        OllamaMonitor monitor,
        HttpContext http,
        CancellationToken ct)
    {
        if (workspaces.Resolve(http) is not { } served)
            return WorkspaceResults.Unknown();

        var documents = await served.Scope.Library.ListAsync(ct);
        var status = await monitor.CheckAsync(ct);

        var ollama = status switch
        {
            OllamaStatus.Ready => "ready",
            OllamaStatus.MissingModels => "missing_models",
            OllamaStatus.Offline => "offline",
            _ => "checking"
        };

        return TypedResults.Ok(new HealthDto(
            ollama == "ready" ? "ok" : "degraded",
            ollama,
            documents.Count,
            documents.Count(d => d.State is IndexState.Ready),
            documents.Count(d => d.State is IndexState.Failed)));
    }
}
