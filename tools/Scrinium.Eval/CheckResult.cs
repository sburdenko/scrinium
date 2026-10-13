namespace Scrinium.Eval;

public abstract record CheckResult(string Name)
{
    public sealed record Passed(string Name) : CheckResult(Name);

    public sealed record Failed(string Name, string Reason) : CheckResult(Name);

    public sealed record Skipped(string Name, string Reason) : CheckResult(Name);
}
