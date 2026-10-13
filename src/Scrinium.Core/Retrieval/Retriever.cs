using Microsoft.Extensions.AI;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed record RetrievalOptions(
    int TopK = 6,
    int ContextTokenBudget = 3000,
    int PreviewLength = 220,
    double MinScore = 0.35,
    double ScoreMargin = 0.10);

public sealed class Retriever(
    IVectorStore vectors,
    IDocumentStore documents,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings,
    RetrievalOptions options)
{
    public async Task<RetrievalOutcome> RetrieveAsync(string query, CancellationToken ct = default)
    {
        var model = settings.Current.EmbeddingModel;
        var generated = await embedder.GenerateAsync([query], new EmbeddingGenerationOptions { ModelId = model }, ct);
        var queryVector = generated[0].Vector.ToArray();
        var candidates = await vectors.SearchAsync(queryVector, model, options.TopK, ct);

        if (candidates.Count == 0)
            return new RetrievalOutcome.ArchiveEmpty();

        var best = candidates[0];
        var relevant = candidates
            .Where(hit => hit.Score >= options.MinScore && hit.Score >= best.Score - options.ScoreMargin)
            .ToList();

        if (relevant.Count == 0)
            return new RetrievalOutcome.NothingRelevant(best.DocumentTitle, best.Score);

        return new RetrievalOutcome.Found(await BuildFragmentsAsync(relevant, queryVector, ct));
    }

    private async Task<IReadOnlyList<ContextFragment>> BuildFragmentsAsync(
        IReadOnlyList<ChunkHit> hits,
        float[] queryVector,
        CancellationToken ct)
    {
        var passages = await LocatePassagesAsync(hits, queryVector, ct);
        var topChunkIds = hits.Select(h => h.Chunk.Id).ToHashSet();
        var fragments = new List<ContextFragment>();
        var budget = 0.0;

        foreach (var hit in hits)
        {
            var text = await BuildPromptTextAsync(hit, topChunkIds, ct);
            var weight = TokenEstimator.Weight(text);
            if (fragments.Count > 0 && budget + weight > options.ContextTokenBudget)
                break;

            budget += weight;
            fragments.Add(new ContextFragment(ToCitation(fragments.Count + 1, hit, passages[hit.Chunk.Id]), text));
        }

        return fragments;
    }

    private async Task<string> BuildPromptTextAsync(ChunkHit hit, HashSet<Guid> topChunkIds, CancellationToken ct)
    {
        var chunk = hit.Chunk;
        var range = await documents.GetChunkRangeAsync(chunk.DocumentId, chunk.Ordinal - 1, chunk.Ordinal + 1, ct);

        var parts = range
            .Where(c => c.Id == chunk.Id || !topChunkIds.Contains(c.Id))
            .Select(c => c.Text);
        return string.Join("\n", parts);
    }

    private async Task<Dictionary<Guid, PassageSpan>> LocatePassagesAsync(
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

    private Citation ToCitation(int number, ChunkHit hit, PassageSpan passage)
    {
        var chunk = hit.Chunk;
        var sentence = chunk.Text.Substring(passage.Start, passage.Length);
        var preview = sentence.Length <= options.PreviewLength
            ? sentence
            : sentence[..options.PreviewLength].TrimEnd() + "…";

        return new Citation(
            number,
            chunk.DocumentId,
            hit.DocumentTitle,
            chunk.Page,
            chunk.Date,
            preview,
            new SourceLocation(chunk.DocumentId, chunk.Page, chunk.Offset + passage.Start, passage.Length));
    }
}
