using System.Globalization;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public sealed record CandidateRowViewModel(string Score, string Fate, string Source, string Document, string Section, CandidateFate Kind)
{
    public bool IsSent => Kind == CandidateFate.Sent;

    public static CandidateRowViewModel From(RetrievalCandidate candidate, RetrievalOptions options) => new(
        candidate.Score.ToString("0.000", CultureInfo.InvariantCulture),
        candidate.Fate switch
        {
            CandidateFate.Sent => "sent",
            CandidateFate.BelowMinScore => "below min score",
            CandidateFate.OutsideMargin => "outside margin",
            CandidateFate.BeyondTopK => $"beyond top {options.TopK}",
            _ => "over token budget"
        },
        candidate.Source switch
        {
            CandidateSource.Meaning => "meaning",
            CandidateSource.Words => "words",
            _ => "meaning and words"
        },
        candidate.DocumentTitle,
        candidate.Section,
        candidate.Fate);
}
