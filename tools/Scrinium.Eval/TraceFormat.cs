using System.Globalization;
using Scrinium.Core.Retrieval;

namespace Scrinium.Eval;

public static class TraceFormat
{
    public static string Score(double? score) =>
        score is { } value ? value.ToString("0.000", CultureInfo.InvariantCulture) : "—";

    public static string Line(RetrievalCandidate candidate)
    {
        var place = string.IsNullOrWhiteSpace(candidate.Section)
            ? candidate.DocumentTitle
            : $"{candidate.DocumentTitle} › {candidate.Section}";
        return $"- {Score(candidate.Score)} {Fate(candidate.Fate)} · {Source(candidate.Source)} · {place}";
    }

    private static string Fate(CandidateFate fate) => fate switch
    {
        CandidateFate.Sent => "sent",
        CandidateFate.BelowMinScore => "below min score",
        CandidateFate.OutsideMargin => "outside margin",
        CandidateFate.BeyondTopK => "beyond top k",
        CandidateFate.OverBudget => "over budget",
        _ => throw new ArgumentOutOfRangeException(nameof(fate), fate, null)
    };

    private static string Source(CandidateSource source) => source switch
    {
        CandidateSource.Meaning => "meaning",
        CandidateSource.Words => "words",
        CandidateSource.MeaningAndWords => "meaning and words",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };
}
