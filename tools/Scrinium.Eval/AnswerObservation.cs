namespace Scrinium.Eval;

public sealed record AnswerObservation(
    string Text,
    IReadOnlyList<string> RetrievedTitles,
    IReadOnlyList<string> CitedTitles,
    TimeSpan Elapsed)
{
    public bool Refused => CitedTitles.Count == 0;
}
