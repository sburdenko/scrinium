namespace Scrinium.Eval;

public sealed record EvaluationCase(string Id, string Question, string? Language, Expectation Expect);
