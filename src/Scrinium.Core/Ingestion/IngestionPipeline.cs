using Microsoft.Extensions.AI;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Ingestion;

public sealed class IngestionPipeline(
    IDocumentStore store,
    IEnumerable<ITextExtractor> extractors,
    TextChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings,
    DataPaths paths)
{
    private const int EmbeddingBatchSize = 32;

    private readonly IReadOnlyList<ITextExtractor> _extractors = extractors.ToList();

    public async Task ProcessAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await store.GetAsync(documentId, ct);
        if (document is null)
            return;

        await store.SetStateAsync(documentId, new IndexState.Indexing(), ct);

        try
        {
            var model = settings.Current.EmbeddingModel;
            var chunks = await BuildChunksAsync(document, model, ct);
            await store.ReplaceChunksAsync(documentId, chunks, model, ct);
        }
        catch (OperationCanceledException)
        {
            await store.SetStateAsync(documentId, new IndexState.Pending(), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await store.SetStateAsync(documentId, new IndexState.Failed(Describe(ex)), CancellationToken.None);
        }
    }

    private async Task<IReadOnlyList<EmbeddedChunk>> BuildChunksAsync(Document document, string model, CancellationToken ct)
    {
        var extractor = _extractors.FirstOrDefault(e => e.Supports(document.Kind))
            ?? throw new TextExtractionException($"Document type {document.Kind} is not supported yet.");

        var segments = await Task.Run(() => extractor.Extract(paths.Resolve(document.StoredPath)), ct);
        var drafts = chunker.Chunk(segments);
        if (drafts.Count == 0)
            throw new TextExtractionException("The document contains no text.");

        var vectors = new List<float[]>(drafts.Count);
        var options = new EmbeddingGenerationOptions { ModelId = model };
        foreach (var batch in drafts.Chunk(EmbeddingBatchSize))
        {
            var generated = await embedder.GenerateAsync(batch.Select(d => d.Text).ToList(), options, ct);
            if (generated.Count != batch.Length)
                throw new InvalidOperationException("The embedding model returned the wrong number of vectors.");

            vectors.AddRange(generated.Select(e => e.Vector.ToArray()));
        }

        return drafts
            .Select((draft, index) => new EmbeddedChunk(
                new Chunk(Guid.NewGuid(), document.Id, index, draft.Page, draft.Offset, draft.Text, document.FileDate, draft.TokenCount),
                vectors[index]))
            .ToList();
    }

    private static string Describe(Exception ex) => ex switch
    {
        TextExtractionException => ex.Message,
        HttpRequestException => $"Could not reach the model: {ex.Message}",
        _ => ex.Message
    };
}
