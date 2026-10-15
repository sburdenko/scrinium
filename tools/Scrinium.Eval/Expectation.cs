namespace Scrinium.Eval;

public abstract record Expectation
{
    private Expectation() { }

    public sealed record Answers(IReadOnlyList<string> Contains, string? Cites, IReadOnlyList<string>? Forbids = null) : Expectation
    {
        /// <summary>
        /// What the highlighted sentence must hold when it differs from the answer's facts, for a table row whose
        /// column names carry the answer or a source in another language than the question.
        /// </summary>
        public IReadOnlyList<string>? Passage { get; init; }
    }

    public sealed record Refuses : Expectation;
}
