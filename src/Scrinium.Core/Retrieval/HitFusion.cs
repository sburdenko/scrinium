using Scrinium.Core.Documents;

namespace Scrinium.Core.Retrieval;

/// <summary>
/// Reciprocal rank fusion of the meaning hits and the word hits. Every hit must reach the floor. Meaning hits must
/// also stay close to the best one; word hits may not, because an exact identifier is the case where the embedding
/// ranks the right chunk too low.
/// </summary>
internal sealed record HitFusion(IReadOnlyList<FusedHit> Kept, IReadOnlyList<RetrievalCandidate> Dropped)
{
    // The usual constant keeps one list's first place from drowning the other list.
    private const int FusionConstant = 60;

    public static HitFusion Of(IReadOnlyList<ChunkHit> byMeaning, IReadOnlyList<ChunkHit> byWords, RetrievalOptions options)
    {
        var best = byMeaning[0].Score;
        var merged = Ranked(byMeaning, CandidateSource.Meaning, hit => MeaningRejection(hit, best, options))
            .Concat(Ranked(byWords, CandidateSource.Words, hit => hit.Score >= options.MinScore ? null : CandidateFate.BelowMinScore))
            .GroupBy(entry => entry.Hit.Chunk.Id)
            .Select(Merge)
            .ToList();

        var ranked = merged
            .Where(entry => entry.Rejection is null)
            .OrderByDescending(entry => entry.Weight)
            .ThenByDescending(entry => entry.Hit.Score)
            .ToList();

        var kept = ranked.Take(options.TopK).Select(entry => new FusedHit(entry.Hit, entry.Source)).ToList();
        var dropped = ranked.Skip(options.TopK).Select(entry => Candidate(entry.Hit, entry.Source, CandidateFate.BeyondTopK))
            .Concat(merged.Where(entry => entry.Rejection is not null).Select(entry => Candidate(entry.Hit, entry.Source, entry.Rejection!.Value)))
            .ToList();

        return new HitFusion(kept, dropped);
    }

    public static RetrievalCandidate Candidate(ChunkHit hit, CandidateSource source, CandidateFate fate) =>
        new(hit.DocumentTitle, hit.Chunk.Section, hit.Score, source, fate);

    /// <summary>The kept hits in front of the budget were sent; the rest did not fit.</summary>
    public IEnumerable<RetrievalCandidate> Candidates(int sent) =>
        Kept.Select((kept, index) => Candidate(kept.Hit, kept.Source, index < sent ? CandidateFate.Sent : CandidateFate.OverBudget))
            .Concat(Dropped);

    private static CandidateFate? MeaningRejection(ChunkHit hit, double best, RetrievalOptions options) =>
        hit.Score < options.MinScore ? CandidateFate.BelowMinScore
        : hit.Score < best - options.ScoreMargin ? CandidateFate.OutsideMargin
        : null;

    private static IEnumerable<Entry> Ranked(IReadOnlyList<ChunkHit> hits, CandidateSource source, Func<ChunkHit, CandidateFate?> rejection) =>
        hits.Select((hit, index) => new Entry(hit, source, 1.0 / (FusionConstant + index + 1), rejection(hit)));

    private static Entry Merge(IGrouping<Guid, Entry> group)
    {
        var passing = group.Where(entry => entry.Rejection is null).ToList();
        var source = group.Select(entry => entry.Source).Distinct().Count() > 1 ? CandidateSource.MeaningAndWords : group.First().Source;
        return passing.Count > 0
            ? new Entry(group.First().Hit, source, passing.Sum(entry => entry.Weight), null)
            : new Entry(group.First().Hit, source, 0, group.First().Rejection);
    }

    /// <param name="Rejection">Why the hit cannot be sent, or none when it passes the floor and the margin.</param>
    private sealed record Entry(ChunkHit Hit, CandidateSource Source, double Weight, CandidateFate? Rejection);
}

internal sealed record FusedHit(ChunkHit Hit, CandidateSource Source);
