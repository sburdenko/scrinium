using Scrinium.Core.Documents;
using Scrinium.Core.Preview;

namespace Scrinium.Server.Endpoints;

internal static class SourcesEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes.MapGet("/api/sources/{documentId:guid}", HandleAsync);

    private static async Task<IResult> HandleAsync(
        Guid documentId,
        int? page,
        int offset,
        int length,
        DocumentPreviewService previews,
        CancellationToken ct)
    {
        if (offset < 0 || length < 0)
            return TypedResults.BadRequest(new ErrorDto("Offset and length must not be negative."));

        var preview = await previews.LoadAsync(new SourceLocation(documentId, page, offset, length), ct);
        if (preview is null)
            return TypedResults.NotFound(new ErrorDto("The document or its original is not available."));

        return TypedResults.Ok(new PreviewDto(
            preview.Title,
            preview.Page,
            preview.Before,
            preview.Highlight,
            preview.After,
            Describe(preview.Truncation)));
    }

    private static string Describe(PreviewTruncation truncation) => truncation switch
    {
        PreviewTruncation.None => "none",
        PreviewTruncation.Start => "start",
        PreviewTruncation.End => "end",
        _ => "both"
    };
}
