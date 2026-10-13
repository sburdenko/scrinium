using Scrinium.Eval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public class LanguageDetectorTests
{
    [Fact]
    public void German_text_is_detected() =>
        Assert.Equal("de", LanguageDetector.Detect("Die Kanzlei ist am Samstag nur nach Terminvereinbarung geöffnet [1]."));

    [Fact]
    public void English_text_is_detected() =>
        Assert.Equal("en", LanguageDetector.Detect("The office is open on Saturdays by appointment only [1]."));

    [Fact]
    public void Cyrillic_text_is_detected() =>
        Assert.Equal("ru", LanguageDetector.Detect("По субботам мы работаем только по записи."));

    [Fact]
    public void Text_without_words_is_unknown() =>
        Assert.Equal("unknown", LanguageDetector.Detect("12:30 [1]"));
}

public class CaseLoaderTests
{
    [Fact]
    public void Answer_and_refusal_cases_are_parsed()
    {
        var cases = CaseLoader.Parse("""
            { "cases": [
              { "id": "a", "question": "Q1", "language": "de", "expect": "answer", "contains": ["10"], "cites": "x.md" },
              { "id": "r", "question": "Q2", "expect": "refusal" }
            ] }
            """);

        var answer = Assert.IsType<Expectation.Answers>(cases[0].Expect);
        Assert.Equal(["10"], answer.Contains);
        Assert.Equal("x.md", answer.Cites);
        Assert.Equal("de", cases[0].Language);
        Assert.IsType<Expectation.Refuses>(cases[1].Expect);
    }

    [Theory]
    [InlineData("""{ "cases": [ { "id": "a", "question": "", "expect": "refusal" } ] }""")]
    [InlineData("""{ "cases": [ { "id": "a", "question": "Q", "expect": "maybe" } ] }""")]
    [InlineData("""{ "cases": [ { "id": "a", "question": "Q", "expect": "answer" } ] }""")]
    [InlineData("""{ "cases": [ { "id": "a", "question": "Q", "expect": "refusal" }, { "id": "a", "question": "Q2", "expect": "refusal" } ] }""")]
    public void Invalid_cases_are_rejected(string json) =>
        Assert.Throws<InvalidDataException>(() => CaseLoader.Parse(json));
}

public class DemoDatasetTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Scrinium.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly string Demo = Path.Combine(RepositoryRoot(), "demo", "steuerberatung-mueller");

    [Fact]
    public void The_dataset_is_valid_and_large_enough()
    {
        var cases = CaseLoader.Load(Path.Combine(Demo, "evaluation.json"));

        Assert.True(cases.Count >= 20);
        Assert.True(cases.Count(c => c.Expect is Expectation.Refuses) >= 3);
        Assert.Contains(cases, c => c.Language == "ru");
        Assert.Contains(cases, c => c.Language == "en");
    }

    [Fact]
    public void Every_cited_document_exists()
    {
        var cases = CaseLoader.Load(Path.Combine(Demo, "evaluation.json"));
        var documents = Directory.GetFiles(Path.Combine(Demo, "documents")).Select(Path.GetFileName).ToHashSet();

        var cited = cases.Select(c => c.Expect).OfType<Expectation.Answers>().Select(a => a.Cites).OfType<string>();

        Assert.All(cited, name => Assert.Contains(name, documents));
    }

    [Fact]
    public void Expected_facts_appear_in_the_cited_documents_for_german_questions()
    {
        var cases = CaseLoader.Load(Path.Combine(Demo, "evaluation.json"));

        foreach (var c in cases.Where(c => c.Language == "de" && c.Expect is Expectation.Answers { Cites: not null }))
        {
            var answers = (Expectation.Answers)c.Expect;
            if (answers.Cites!.EndsWith(".pdf", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(Path.Combine(Demo, "documents", answers.Cites));
            Assert.All(answers.Contains, fact =>
                Assert.True(text.Contains(fact, StringComparison.OrdinalIgnoreCase), $"{c.Id}: '{fact}' is not in {answers.Cites}"));
        }
    }
}

public class AnswerScorerTests
{
    private static EvaluationCase Answer(string[] contains, string? cites = "doc.md", string? language = "en") =>
        new("c", "question", language, new Expectation.Answers(contains, cites));

    private static EvaluationCase Refusal() => new("r", "question", null, new Expectation.Refuses());

    private static AnswerObservation Observed(string text, params string[] cited) =>
        new(text, cited, cited, TimeSpan.FromSeconds(1));

    private static bool AllPassed(IEnumerable<CheckResult> checks) => checks.All(c => c is CheckResult.Passed);

    [Fact]
    public void A_correct_cited_answer_in_the_right_language_passes()
    {
        var checks = AnswerScorer.Score(Answer(["10", "13"]), Observed("The office opens at 10 and closes at 13 [1] and the team is here.", "doc.md"));

        Assert.True(AllPassed(checks));
    }

    [Fact]
    public void A_missing_fact_fails_and_names_it()
    {
        var checks = AnswerScorer.Score(Answer(["10", "13"]), Observed("The office opens at 10 [1] and we are here.", "doc.md"));

        var failed = Assert.Single(checks.OfType<CheckResult.Failed>());
        Assert.Equal("facts", failed.Name);
        Assert.Contains("13", failed.Reason);
    }

    [Fact]
    public void Facts_are_matched_without_case_and_with_non_breaking_spaces()
    {
        var checks = AnswerScorer.Score(Answer(["vier Wochen"], language: null), Observed("Die Frist beträgt Vier Wochen [1].", "doc.md"));

        Assert.True(AllPassed(checks));
    }

    [Fact]
    public void Citing_the_wrong_document_fails_the_source_check()
    {
        var checks = AnswerScorer.Score(Answer(["10"], language: null), Observed("It is 10 [1].", "other.md"));

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "source");
    }

    [Fact]
    public void Refusing_an_answerable_question_fails()
    {
        var observation = new AnswerObservation("Not mentioned.", ["doc.md"], [], TimeSpan.FromSeconds(1));

        var checks = AnswerScorer.Score(Answer(["10"], language: null), observation);

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "answered");
    }

    [Fact]
    public void A_wrong_language_fails()
    {
        var checks = AnswerScorer.Score(Answer(["10"], language: "ru"), Observed("The office opens at 10 and we are here today [1].", "doc.md"));

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "language");
    }

    [Fact]
    public void An_answer_too_short_to_judge_the_language_is_skipped_not_failed()
    {
        var checks = AnswerScorer.Score(Answer(["24"], language: "de"), Observed("24,98 [1]", "doc.md"));

        Assert.Contains(checks.OfType<CheckResult.Skipped>(), s => s.Name == "language");
        Assert.DoesNotContain(checks, c => c is CheckResult.Failed);
    }

    [Fact]
    public void An_expected_refusal_passes_when_nothing_is_cited()
    {
        var observation = new AnswerObservation("Not in the documents.", [], [], TimeSpan.FromSeconds(1));

        Assert.True(AllPassed(AnswerScorer.Score(Refusal(), observation)));
    }

    [Fact]
    public void An_expected_refusal_fails_when_the_model_cites_a_source()
    {
        var checks = AnswerScorer.Score(Refusal(), Observed("Dogs are welcome [1].", "doc.md"));

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "refusal");
    }
}

public sealed class EvaluationRunnerTests : IDisposable
{
    private readonly TestApp _app = new(new FakeChatClient("Engine noise ", "got louder [1]."));

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task A_case_is_run_through_the_real_answer_pipeline_and_scored()
    {
        var file = _app.WriteFile("engine.txt", "Engine noise got louder in September.");
        var runner = new EvaluationRunner(_app.Library, _app.Pipeline, _app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(
            new EvaluationCase("engine", "engine noise", "en", new Expectation.Answers(["louder"], "engine.txt")));

        Assert.True(result.Passed);
        Assert.Equal(["engine.txt"], result.Observation.CitedTitles);
        Assert.Contains("louder", result.Observation.Text);
    }

    [Fact]
    public async Task A_question_about_nothing_is_scored_as_a_refusal()
    {
        var file = _app.WriteFile("engine.txt", "Engine noise got louder in September.");
        var runner = new EvaluationRunner(_app.Library, _app.Pipeline, _app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(new EvaluationCase("cat", "red fluffy cat milk", null, new Expectation.Refuses()));

        Assert.True(result.Passed);
        Assert.Empty(result.Observation.CitedTitles);
    }
}

public class EvaluationReportTests
{
    private static CaseResult Result(string id, bool passed, double seconds)
    {
        var testCase = new EvaluationCase(id, "q " + id, null, new Expectation.Refuses());
        var observation = new AnswerObservation("answer text of " + id, [], passed ? [] : ["x.md"], TimeSpan.FromSeconds(seconds));
        return new CaseResult(testCase, observation, AnswerScorer.Score(testCase, observation));
    }

    [Fact]
    public void The_summary_counts_cases_and_reports_the_median_time()
    {
        var summary = EvaluationSummary.From([Result("a", true, 1), Result("b", true, 3), Result("c", false, 2)]);

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Passed);
        Assert.Equal(TimeSpan.FromSeconds(2), summary.MedianTime);
    }

    [Fact]
    public void The_markdown_report_lists_every_case_and_the_failure_reason()
    {
        var run = new EvaluationRun("Demo", new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero), "chat-model", "embed-model", 3,
            [Result("ok-case", true, 1), Result("bad-case", false, 2)]);

        var markdown = ReportWriter.Markdown(run);

        Assert.Contains("chat-model", markdown);
        Assert.Contains("ok-case", markdown);
        Assert.Contains("bad-case", markdown);
        Assert.Contains("1 / 2", markdown);
    }

    [Fact]
    public void Failed_cases_show_the_question_and_the_answer_and_passed_ones_do_not()
    {
        var run = new EvaluationRun("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1,
            [Result("ok-case", true, 1), Result("bad-case", false, 2)]);

        var markdown = ReportWriter.Markdown(run);

        Assert.Contains("answer text of bad-case", markdown);
        Assert.Contains("q bad-case", markdown);
        Assert.DoesNotContain("answer text of ok-case", markdown);
    }
}
