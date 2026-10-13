namespace Scrinium.Eval;

public sealed record EvaluationSummary(
    int Total,
    int Passed,
    int Answerable,
    int FactsCorrect,
    int SourcesCorrect,
    int SourcesChecked,
    int Refusals,
    int RefusalsCorrect,
    int LanguageChecked,
    int LanguageCorrect,
    TimeSpan MedianTime,
    TimeSpan SlowestTime)
{
    public static EvaluationSummary From(IReadOnlyList<CaseResult> results)
    {
        var answerable = results.Where(r => r.Case.Expect is Expectation.Answers).ToList();
        var refusals = results.Where(r => r.Case.Expect is Expectation.Refuses).ToList();
        var times = results.Select(r => r.Observation.Elapsed).Order().ToList();

        return new EvaluationSummary(
            results.Count,
            results.Count(r => r.Passed),
            answerable.Count,
            answerable.Count(r => CheckPassed(r, "facts")),
            answerable.Count(r => CheckPassed(r, "source")),
            answerable.Count(r => HasCheck(r, "source")),
            refusals.Count,
            refusals.Count(r => CheckPassed(r, "refusal")),
            results.Count(r => HasCheck(r, "language")),
            results.Count(r => CheckPassed(r, "language")),
            times.Count == 0 ? TimeSpan.Zero : times[times.Count / 2],
            times.Count == 0 ? TimeSpan.Zero : times[^1]);
    }

    private static bool HasCheck(CaseResult result, string check) => result.Checks.Any(c => c.Name == check && c is not CheckResult.Skipped);

    private static bool CheckPassed(CaseResult result, string check) =>
        result.Checks.Any(c => c.Name == check && c is CheckResult.Passed);
}
