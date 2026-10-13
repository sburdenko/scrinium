using Microsoft.Extensions.AI;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed class Retriever(
    IVectorStore vectors,
    IDocumentStore documents,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings,
    PassageLocator passageLocator,
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
        var passages = await passageLocator.LocateAsync(hits, queryVector, ct);
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
            fragments.Add(new ContextFragment(
                ToCitation(fragments.Count + 1, hit, passages[hit.Chunk.Id]),
                text,
                hit.Chunk.Text,
                hit.Chunk.Offset));
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
        return MarkdownTables.Flatten(string.Join("\n", parts));
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
