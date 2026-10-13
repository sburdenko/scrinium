using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Server.Security;

namespace Scrinium.Server.Endpoints;

internal static class DocumentsEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/api/documents", HandleAsync).AddEndpointFilter<ApiKeyFilter>();

    private static async Task<IResult> HandleAsync(DocumentLibrary library, CancellationToken ct)
    {
        var documents = await library.ListAsync(ct);
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
