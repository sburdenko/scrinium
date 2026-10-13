namespace Scrinium.Eval;

public abstract record Expectation
{
    private Expectation() { }

    public sealed record Answers(IReadOnlyList<string> Contains, string? Cites, IReadOnlyList<string>? Forbids = null) : Expectation;

    public sealed record Refuses : Expectation;
}
