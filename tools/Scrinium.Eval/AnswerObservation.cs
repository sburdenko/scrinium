namespace Scrinium.Eval;

public sealed record AnswerObservation(
    string Text,
    IReadOnlyList<string> RetrievedTitles,
    IReadOnlyList<string> CitedTitles,
    TimeSpan Elapsed,
    IReadOnlyList<string>? CitedPassages = null,
    IReadOnlyList<string>? RetrievedLines = null)
{
    public bool Refused => CitedTitles.Count == 0;
}
