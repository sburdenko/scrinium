using Scrinium.Core.Documents;
using Scrinium.Server.Security;

namespace Scrinium.Server.Endpoints;

internal static class DocumentsEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/documents", HandleAsync).AddEndpointFilter<ApiKeyFilter>();

    private static async Task<IResult> HandleAsync(ServerWorkspaces workspaces, HttpContext http, CancellationToken ct)
    {
        if (workspaces.Resolve(http) is not { } served)
            return WorkspaceResults.Unknown();

        var documents = await served.Scope.Library.ListAsync(ct);
        return TypedResults.Ok(documents.Select(ToDto).ToList());
    }

    private static DocumentDto ToDto(Document document)
    {
        var (state, chunks, error) = document.State switch
        {
            IndexState.Ready ready => ("ready", ready.ChunkCount, (string?)null),
            IndexState.Failed failed => ("failed", 0, failed.Error),
            IndexState.Indexing => ("indexing", 0, null),
            _ => ("pending", 0, null)
        };

        return new DocumentDto(document.Id, document.Title, document.Kind.ToString().ToLowerInvariant(), state, chunks, error);
    }
}
