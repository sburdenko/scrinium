using Scrinium.Core.Chat;

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
                if (observed.CitedPassages is not null && answers.Contains.Count > 0)
                    checks.Add(Passage(answers, observed.CitedPassages));
                if (answers.Forbids is { Count: > 0 })
                    checks.Add(Forbidden(answers.Forbids, observed));
                break;
        }

        if (testCase.Language is { } language && testCase.Expect is Expectation.Answers)
            checks.Add(Language(language, observed));

        return checks;
    }

    private static CheckResult Facts(Expectation.Answers answers, AnswerObservation observed)
    {
        var text = Normalize(observed.Text);
        var missing = answers.Contains.Where(fact => !Present(text, fact)).ToList();

        return missing.Count == 0
            ? new CheckResult.Passed("facts")
            : new CheckResult.Failed("facts", "missing: " + string.Join(", ", missing));
    }

    private static bool Present(string normalizedText, string fact) =>
        fact.Split('|').Any(variant => normalizedText.Contains(Normalize(variant.Trim()), StringComparison.OrdinalIgnoreCase));

    private static CheckResult Passage(Expectation.Answers answers, IReadOnlyList<string> passages)
    {
        var text = Normalize(string.Join(" ", passages));
        return answers.Contains.Any(fact => Present(text, fact))
            ? new CheckResult.Passed("passage")
            : new CheckResult.Failed("passage", "the highlighted sentence holds none of the expected facts");
    }

    private static CheckResult Forbidden(IReadOnlyList<string> forbidden, AnswerObservation observed)
    {
        var text = Normalize(observed.Text);
        var found = forbidden.Where(word => text.Contains(Normalize(word), StringComparison.OrdinalIgnoreCase)).ToList();

        return found.Count == 0
            ? new CheckResult.Passed("forbidden")
            : new CheckResult.Failed("forbidden", "contains: " + string.Join(", ", found));
    }

    private static CheckResult Source(string expected, AnswerObservation observed)
    {
        var acceptable = expected.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hit = observed.CitedTitles.Any(title => acceptable.Contains(title, StringComparer.OrdinalIgnoreCase));

        return hit
            ? new CheckResult.Passed("source")
            : new CheckResult.Failed("source", $"expected {string.Join(" or ", acceptable)}, cited {(observed.CitedTitles.Count == 0 ? "nothing" : string.Join(", ", observed.CitedTitles))}");
    }

    private static CheckResult Language(string expected, AnswerObservation observed)
    {
        var detected = LanguageDetector.Detect(observed.Text);
        if (detected == LanguageDetector.Unknown)
        {
            return LanguageDetector.WordCount(observed.Text) >= LanguageDetector.EnoughWords
                ? new CheckResult.Failed("language", $"expected {expected}, but no word of {expected} was recognised")
                : new CheckResult.Skipped("language", "the answer is too short to tell");
        }

        return detected == expected
            ? new CheckResult.Passed("language")
            : new CheckResult.Failed("language", $"expected {expected}, detected {detected}");
    }

    private static string Normalize(string text) => text.Replace(' ', ' ');
}
