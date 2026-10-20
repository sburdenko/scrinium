using System.Text;
using Microsoft.Extensions.AI;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed class Retriever(
    IVectorStore vectors,
    IWordIndex words,
    IDocumentStore documents,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings,
    PassageLocator passageLocator,
    RetrievalOptions options)
{
    // Reciprocal rank fusion: the usual constant keeps one list's first place from drowning the other list.
    private const int FusionConstant = 60;

    public async Task<RetrievalOutcome> RetrieveAsync(string query, CancellationToken ct = default)
    {
        var model = settings.Current.EmbeddingModel;
        var generated = await embedder.GenerateAsync([query], new EmbeddingGenerationOptions { ModelId = model }, ct);
        var queryVector = generated[0].Vector.ToArray();
        var byMeaning = await vectors.SearchAsync(queryVector, model, options.TopK, ct);

        if (byMeaning.Count == 0)
            return new RetrievalOutcome.ArchiveEmpty();

        var best = byMeaning[0];
        if (best.Score < options.MinScore)
            return new RetrievalOutcome.NothingRelevant(best.DocumentTitle, best.Score);

        var byWords = await FindByWordsAsync(query, queryVector, model, ct);
        return new RetrievalOutcome.Found(await BuildFragmentsAsync(Fuse(byMeaning, byWords, best.Score), queryVector, ct));
    }

    private async Task<IReadOnlyList<ChunkHit>> FindByWordsAsync(string query, float[] queryVector, string model, CancellationToken ct)
    {
        var terms = options.Search == SearchMode.Hybrid ? SearchTerms.Identifiers(query) : [];
        if (terms.Count == 0)
            return [];

        var normalized = VectorMath.Normalize(queryVector);
        var matches = await words.FindAsync(terms, model, options.TopK, ct);
        return matches
            .Select(match => new ChunkHit(match.Chunk, match.DocumentTitle, VectorMath.Dot(normalized, match.Embedding)))
            .Where(hit => !double.IsNegativeInfinity(hit.Score))
            .ToList();
    }

    /// <summary>
    /// Every hit must reach the floor. Meaning hits must also stay close to the best one; word hits may not, because
    /// an exact identifier is the case where the embedding ranks the right chunk too low.
    /// </summary>
    private IReadOnlyList<ChunkHit> Fuse(IReadOnlyList<ChunkHit> byMeaning, IReadOnlyList<ChunkHit> byWords, double best) =>
        Ranked(byMeaning, hit => hit.Score >= options.MinScore && hit.Score >= best - options.ScoreMargin)
            .Concat(Ranked(byWords, hit => hit.Score >= options.MinScore))
            .GroupBy(entry => entry.Hit.Chunk.Id)
            .Select(group => (group.First().Hit, Weight: group.Sum(entry => entry.Weight)))
            .OrderByDescending(entry => entry.Weight)
            .ThenByDescending(entry => entry.Hit.Score)
            .Take(options.TopK)
            .Select(entry => entry.Hit)
            .ToList();

    private static IEnumerable<(ChunkHit Hit, double Weight)> Ranked(IReadOnlyList<ChunkHit> hits, Func<ChunkHit, bool> keep) =>
        hits.Select((hit, index) => (Hit: hit, Weight: 1.0 / (FusionConstant + index + 1))).Where(entry => keep(entry.Hit));

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
            var (promptText, chunkText, chunkOffset) = await BuildWindowAsync(hit, topChunkIds, ct);
            var weight = TokenEstimator.Weight(promptText);
            if (fragments.Count > 0 && budget + weight > options.ContextTokenBudget)
                break;

            budget += weight;
            fragments.Add(new ContextFragment(
                ToCitation(fragments.Count + 1, hit, passages[hit.Chunk.Id]),
                promptText,
                chunkText,
                chunkOffset,
                hit.Chunk.Section,
                hit.Chunk.Content));
        }

        return fragments;
    }

    private async Task<(string PromptText, string ChunkText, int ChunkOffset)> BuildWindowAsync(
        ChunkHit hit,
        HashSet<Guid> topChunkIds,
        CancellationToken ct)
    {
        var chunk = hit.Chunk;
        var range = await documents.GetChunkRangeAsync(chunk.DocumentId, chunk.Ordinal - 1, chunk.Ordinal + 1, ct);
        var parts = range
            .Where(c => c.Id == chunk.Id || !topChunkIds.Contains(c.Id))
            .ToList();

        var (stitched, offset) = StitchSamePage(parts, chunk);
        var promptParts = parts
            .GroupBy(c => c.Page)
            .Select(group => group.Key == chunk.Page ? stitched : string.Join("\n", group.Select(c => c.Text)));
        return (MarkdownTables.Flatten(string.Join("\n", promptParts)), stitched, offset);
    }

    private static (string Text, int Offset) StitchSamePage(IReadOnlyList<Chunk> parts, Chunk center)
    {
        var samePage = parts.Where(c => c.Page == center.Page).OrderBy(c => c.Offset).ToList();
        if (samePage.Count == 0)
            return (center.Text, center.Offset);

        var offset = samePage[0].Offset;
        var builder = new StringBuilder(samePage[0].Text);
        foreach (var next in samePage.Skip(1))
        {
            var end = offset + builder.Length;
            if (next.Offset <= end)
            {
                var overlap = end - next.Offset;
                if (overlap < next.Text.Length)
                    builder.Append(next.Text.AsSpan(overlap));
            }
            else
            {
                builder.Append('\n').Append(' ', next.Offset - end - 1).Append(next.Text);
            }
        }

        return (builder.ToString(), offset);
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
