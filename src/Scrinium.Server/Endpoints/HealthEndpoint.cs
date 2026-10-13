using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Ollama;

namespace Scrinium.Server.Endpoints;

internal static class HealthEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes.MapGet("/api/health", HandleAsync);

    private static async Task<IResult> HandleAsync(DocumentLibrary library, OllamaMonitor monitor, CancellationToken ct)
    {
        var documents = await library.ListAsync(ct);
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
