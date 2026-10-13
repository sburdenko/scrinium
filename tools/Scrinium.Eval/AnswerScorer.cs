namespace Scrinium.Eval;

public static class AnswerScorer
{
    public static IReadOnlyList<CheckResult> Score(EvaluationCase testCase, AnswerObservation observed)
    {
        var checks = new List<CheckResult>();

        switch (testCase.Expect)
        {
            case Expectation.Refuses:
                checks.Add(observed.Refused
                    ? new CheckResult.Passed("refusal")
                    : new CheckResult.Failed("refusal", $"expected a refusal but cited {string.Join(", ", observed.CitedTitles)}"));
                break;

            case Expectation.Answers answers:
                checks.Add(observed.Refused
                    ? new CheckResult.Failed("answered", "the answer cites no source")
                    : new CheckResult.Passed("answered"));
                checks.Add(Facts(answers, observed));
                if (answers.Cites is not null)
                    checks.Add(Source(answers.Cites, observed));
                break;
        }

        if (testCase.Language is { } language && testCase.Expect is Expectation.Answers)
            checks.Add(Language(language, observed));

        return checks;
    }

    private static CheckResult Facts(Expectation.Answers answers, AnswerObservation observed)
    {
        var text = Normalize(observed.Text);
        var missing = answers.Contains.Where(fact => !text.Contains(Normalize(fact), StringComparison.OrdinalIgnoreCase)).ToList();

        return missing.Count == 0
            ? new CheckResult.Passed("facts")
            : new CheckResult.Failed("facts", "missing: " + string.Join(", ", missing));
    }

    private static CheckResult Source(string expected, AnswerObservation observed) =>
        observed.CitedTitles.Contains(expected, StringComparer.OrdinalIgnoreCase)
            ? new CheckResult.Passed("source")
            : new CheckResult.Failed("source", $"expected {expected}, cited {(observed.CitedTitles.Count == 0 ? "nothing" : string.Join(", ", observed.CitedTitles))}");

    private static CheckResult Language(string expected, AnswerObservation observed)
    {
        var detected = LanguageDetector.Detect(observed.Text);
        if (detected == "unknown")
            return new CheckResult.Skipped("language", "the answer is too short to tell");

        return detected == expected
            ? new CheckResult.Passed("language")
            : new CheckResult.Failed("language", $"expected {expected}, detected {detected}");
    }

    private static string Normalize(string text) => text.Replace(' ', ' ');
}
