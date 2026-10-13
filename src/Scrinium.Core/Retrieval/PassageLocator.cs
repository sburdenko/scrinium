using Microsoft.Extensions.AI;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed class PassageLocator(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings)
{
    public async Task<IReadOnlyDictionary<Guid, PassageSpan>> LocateAsync(
        IReadOnlyList<ChunkHit> hits,
        float[] queryVector,
        CancellationToken ct)
    {
        var passages = hits.ToDictionary(h => h.Chunk.Id, h => new PassageSpan(0, h.Chunk.Text.Length));
        var candidates = hits
            .Select(h => (Hit: h, Spans: SentenceSplitter.Split(h.Chunk.Text)))
            .Where(c => c.Spans.Count > 1)
            .ToList();

        if (candidates.Count == 0)
            return passages;

        var texts = candidates
            .SelectMany(c => c.Spans.Select(span => c.Hit.Chunk.Text.Substring(span.Start, span.Length)))
            .ToList();

        GeneratedEmbeddings<Embedding<float>> embeddings;
        try
        {
            var model = settings.Current.EmbeddingModel;
            embeddings = await embedder.GenerateAsync(texts, new EmbeddingGenerationOptions { ModelId = model }, ct);
        }
        catch (HttpRequestException)
        {
            // Highlighting is a nicety: when the model server drops out here, cite the whole chunk.
            return passages;
        }

        var query = VectorMath.Normalize(queryVector);
        var next = 0;
        foreach (var (hit, spans) in candidates)
        {
            var scored = spans
                .Select(span => (Span: span, Score: VectorMath.Dot(query, VectorMath.Normalize(embeddings[next++].Vector.Span))))
                .ToList();
            passages[hit.Chunk.Id] = scored.MaxBy(s => s.Score).Span;
        }

        return passages;
    }
}
