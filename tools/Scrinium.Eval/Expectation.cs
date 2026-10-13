namespace Scrinium.Eval;

public abstract record Expectation
{
    private Expectation() { }

    public sealed record Answers(IReadOnlyList<string> Contains, string? Cites) : Expectation;

    public sealed record Refuses : Expectation;
}
