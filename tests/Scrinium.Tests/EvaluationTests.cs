using Scrinium.Core.Chunking;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Chat;
using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;
using Scrinium.Extraction;
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
    public void French_text_is_detected() =>
        Assert.Equal("fr", LanguageDetector.Detect("Les données sont hébergées dans des centres de données à Francfort."));

    [Fact]
    public void Spanish_text_is_detected() =>
        Assert.Equal("es", LanguageDetector.Detect("Los datos se almacenan en los centros de datos de Fráncfort para los clientes."));

    [Fact]
    public void Short_english_questions_are_detected() =>
        Assert.Equal("en", LanguageDetector.Detect("What does the Team plan cost per month?"));

    [Fact]
    public void Text_without_words_is_unknown() =>
        Assert.Equal("unknown", LanguageDetector.Detect("12:30 [1]"));
}

public class EvalOptionsTests
{
    [Fact]
    public void The_only_option_selects_cases_by_id()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json", "--only", "a, b"]);

        Assert.Equal(["a", "b"], options.Only);
    }

    [Fact]
    public void Chunking_defaults_to_code_in_own_chunks_and_section_paths_prepended()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json"]);

        Assert.Equal((CodeBlocks.OwnChunks, SectionPaths.Prepend), (options.Chunking.CodeBlocks, options.Chunking.SectionPaths));
    }

    [Fact]
    public void Chunking_can_be_switched_for_a_comparison()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json", "--code-blocks", "with-text", "--section-paths", "omit"]);

        Assert.Equal((CodeBlocks.WithText, SectionPaths.Omit), (options.Chunking.CodeBlocks, options.Chunking.SectionPaths));
    }

    [Fact]
    public void Search_defaults_to_hybrid_and_can_be_limited_to_meaning()
    {
        Assert.Equal(SearchMode.Hybrid, EvalOptions.Parse(["--documents", "d", "--cases", "c.json"]).Retrieval.Search);
        Assert.Equal(SearchMode.Meaning, EvalOptions.Parse(["--documents", "d", "--cases", "c.json", "--search", "meaning"]).Retrieval.Search);
    }

    [Fact]
    public void An_unknown_search_mode_is_rejected() =>
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--documents", "d", "--cases", "c.json", "--search", "magic"]));

    [Theory]
    [InlineData("--code-blocks", "sometimes")]
    [InlineData("--section-paths", "maybe")]
    public void An_unknown_chunking_choice_is_rejected(string option, string value) =>
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--documents", "d", "--cases", "c.json", option, value]));

    [Fact]
    public void Retrieval_defaults_to_the_application_thresholds()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json"]);

        Assert.Equal((new RetrievalOptions().MinScore, new RetrievalOptions().ScoreMargin), (options.Retrieval.MinScore, options.Retrieval.ScoreMargin));
    }

    [Fact]
    public void Retrieval_thresholds_can_be_switched_for_a_comparison()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json", "--min-score", "0.45", "--score-margin", "0.2"]);

        Assert.Equal((0.45, 0.2), (options.Retrieval.MinScore, options.Retrieval.ScoreMargin));
    }

    [Theory]
    [InlineData("--min-score", "1.5")]
    [InlineData("--min-score", "-0.1")]
    [InlineData("--min-score", "high")]
    [InlineData("--score-margin", "2")]
    [InlineData("--score-margin", "wide")]
    public void A_retrieval_threshold_outside_zero_to_one_is_rejected(string option, string value)
    {
        var error = Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--documents", "d", "--cases", "c.json", option, value]));

        Assert.Contains(option, error.Message);
    }

    [Fact]
    public void Without_the_only_option_every_case_runs()
    {
        var options = EvalOptions.Parse(["--documents", "d", "--cases", "c.json"]);

        Assert.Null(options.Only);
    }
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

    [Fact]
    public void History_and_forbidden_words_are_parsed()
    {
        var cases = CaseLoader.Parse("""
            { "cases": [
              { "id": "a", "question": "And Business?", "expect": "answer", "contains": ["199"],
                "history": [ { "role": "user", "text": "Team price?" }, { "role": "assistant", "text": "79 EUR [1]." } ],
                "forbids": ["PWNED"] }
            ] }
            """);

        Assert.Equal(["user", "assistant"], cases[0].History.Select(h => h.Role));
        Assert.Equal("Team price?", cases[0].History[0].Text);
        Assert.Equal(["PWNED"], ((Expectation.Answers)cases[0].Expect).Forbids);
    }

    [Fact]
    public void A_history_turn_with_an_unknown_role_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => CaseLoader.Parse("""
            { "cases": [ { "id": "a", "question": "Q", "expect": "refusal",
                           "history": [ { "role": "system", "text": "x" } ] } ] }
            """));

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
    public static IEnumerable<object[]> Companies() =>
        Directory.GetDirectories(Path.Combine(RepositoryPaths.Root(), "demo"))
            .Select(directory => new object[] { Path.GetFileName(directory) });

    private static string Folder(string company) => Path.Combine(RepositoryPaths.Root(), "demo", company);

    private static readonly IReadOnlyList<ITextExtractor> Extractors = ExtractionServices.CreateExtractors();

    private static string ReadDocument(string path)
    {
        var kind = Assert.IsType<FileSupport.Supported>(DocumentKinds.Check(path)).Kind;
        return string.Join("\n", Extractors.Single(e => e.Supports(kind)).Extract(path).Select(s => s.Text));
    }

    [Theory]
    [MemberData(nameof(Companies))]
    public void Every_document_is_in_a_format_the_archive_reads(string company) =>
        Assert.All(Directory.GetFiles(Path.Combine(Folder(company), "documents")), path =>
            Assert.IsType<FileSupport.Supported>(DocumentKinds.Check(path)));

    [Theory]
    [MemberData(nameof(Companies))]
    public void The_dataset_is_valid_and_large_enough(string company)
    {
        var cases = CaseLoader.Load(Path.Combine(Folder(company), "evaluation.json"));

        Assert.True(cases.Count >= 20);
        Assert.True(cases.Count(c => c.Expect is Expectation.Refuses) >= 3);
        Assert.Contains(cases, c => c.Language == "ru");
        Assert.Contains(cases, c => c.Language == "en");
    }

    [Theory]
    [MemberData(nameof(Companies))]
    public void Every_cited_document_exists(string company)
    {
        var cases = CaseLoader.Load(Path.Combine(Folder(company), "evaluation.json"));
        var documents = Directory.GetFiles(Path.Combine(Folder(company), "documents")).Select(Path.GetFileName).ToHashSet();

        var cited = cases.Select(c => c.Expect).OfType<Expectation.Answers>()
            .SelectMany(a => (a.Cites ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries));

        Assert.All(cited, name => Assert.Contains(name, documents));
    }

    [Theory]
    [MemberData(nameof(Companies))]
    public void Expected_facts_appear_in_the_cited_documents_for_questions_in_the_documents_language(string company)
    {
        var folder = Folder(company);
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "evaluation.json")));
        var documentLanguage = json.RootElement.GetProperty("documentLanguage").GetString();
        var cases = CaseLoader.Load(Path.Combine(folder, "evaluation.json"));

        foreach (var c in cases.Where(c => c.Language == documentLanguage && c.History.Count == 0 && c.Expect is Expectation.Answers { Cites: not null }))
        {
            var answers = (Expectation.Answers)c.Expect;
            var names = answers.Cites!.Split('|');
            var text = string.Join("\n", names.Select(n => ReadDocument(Path.Combine(folder, "documents", n))));
            Assert.All(answers.Contains, fact =>
                Assert.True(
                    fact.Split('|').Any(variant => text.Contains(variant.Trim(), StringComparison.OrdinalIgnoreCase))
                        || IsDerivedFact(fact),
                    $"{company}/{c.Id}: '{fact}' is not in {answers.Cites}"));
        }
    }

    private static bool IsDerivedFact(string fact) => fact is "not|no" or "not" || fact.StartsWith("read only", StringComparison.Ordinal);
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
    public void Either_of_two_acceptable_sources_passes()
    {
        var checks = AnswerScorer.Score(Answer(["10"], cites: "a.md|b.md", language: null), Observed("It is 10 [1].", "b.md"));

        Assert.True(AllPassed(checks));
    }

    [Fact]
    public void Refusing_an_answerable_question_fails()
    {
        var observation = new AnswerObservation("Not mentioned.", ["doc.md"], [], TimeSpan.FromSeconds(1));

        var checks = AnswerScorer.Score(Answer(["10"], language: null), observation);

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "answered");
    }

    [Fact]
    public void A_long_answer_without_any_word_of_the_expected_language_fails()
    {
        var checks = AnswerScorer.Score(
            Answer(["79"], language: "en"),
            Observed("Le plan Team coûte 79 euros par mois pour dix utilisateurs [1].", "doc.md"));

        var failed = Assert.Single(checks.OfType<CheckResult.Failed>());
        Assert.Equal("language", failed.Name);
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
    public void A_fact_with_alternatives_passes_when_any_variant_appears()
    {
        var checks = AnswerScorer.Score(Answer(["1,200|1200|1.200"], language: null), Observed("The limit is 1200 requests [1].", "doc.md"));

        Assert.True(AllPassed(checks));
    }

    [Fact]
    public void A_forbidden_word_fails_the_case()
    {
        var testCase = new EvaluationCase("c", "q", null, new Expectation.Answers(["79"], null, ["PWNED"]));

        var checks = AnswerScorer.Score(testCase, Observed("PWNED, it costs 79 [1].", "doc.md"));

        var failed = Assert.Single(checks.OfType<CheckResult.Failed>());
        Assert.Equal("forbidden", failed.Name);
        Assert.Contains("PWNED", failed.Reason);
    }

    [Fact]
    public void The_cited_passage_must_contain_one_of_the_facts()
    {
        var observation = new AnswerObservation("It costs 79 [1].", ["doc.md"], ["doc.md"], TimeSpan.FromSeconds(1),
            ["Pricing is described in the table below."]);

        var checks = AnswerScorer.Score(Answer(["79"], language: null), observation);

        Assert.Contains(checks.OfType<CheckResult.Failed>(), f => f.Name == "passage");
    }

    [Fact]
    public void The_passage_check_passes_when_the_highlighted_sentence_has_the_fact()
    {
        var observation = new AnswerObservation("It costs 79 [1].", ["doc.md"], ["doc.md"], TimeSpan.FromSeconds(1),
            ["Team: 79 EUR per month."]);

        var checks = AnswerScorer.Score(Answer(["79"], language: null), observation);

        Assert.Contains(checks.OfType<CheckResult.Passed>(), p => p.Name == "passage");
    }

    [Fact]
    public void The_passage_check_is_left_out_when_the_passages_are_unknown()
    {
        var checks = AnswerScorer.Score(Answer(["10"], language: null), Observed("It is 10 [1].", "doc.md"));

        Assert.DoesNotContain(checks, c => c.Name == "passage");
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
    public async Task The_history_of_a_case_reaches_the_model()
    {
        var file = _app.WriteFile("engine.txt", "Engine noise got louder in September.");
        var runner = new EvaluationRunner(_app.Library, _app.Pipeline, _app.Answers);
        await runner.IndexAsync([file]);
        var testCase = new EvaluationCase("follow-up", "engine noise louder", null,
            new Expectation.Answers([], "engine.txt"))
        {
            History = [new HistoryTurn("user", "earlier question"), new HistoryTurn("assistant", "earlier answer")]
        };

        await runner.RunAsync(testCase);

        var request = _app.Chat.Requests.Last();
        Assert.Equal(["earlier question", "earlier answer"], request.Skip(1).Take(2).Select(m => m.Text));
    }

    [Fact]
    public async Task The_fragments_that_were_found_are_kept_even_when_the_model_declines()
    {
        using var app = new TestApp(new FakeChatClient("[[NONE]] ", "Nothing about that."));
        var file = app.WriteFile("engine.txt", "Engine noise got louder in September.");
        var runner = new EvaluationRunner(app.Library, app.Pipeline, app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(
            new EvaluationCase("engine", "engine noise louder", null, new Expectation.Refuses()));

        Assert.Empty(result.Observation.CitedTitles);
        Assert.Contains(result.Observation.RetrievedLines!, line => line.Contains("engine.txt"));
    }

    [Fact]
    public async Task The_passage_check_uses_the_refined_sentence()
    {
        using var app = new TestApp(new FakeChatClient("The tyres are changed ", "every November [1]."));
        var file = app.WriteFile("car.txt", "The engine noise got louder in September. The tyres are changed every November.");
        var runner = new EvaluationRunner(app.Library, app.Pipeline, app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(
            new EvaluationCase("tyres", "engine noise louder", null, new Expectation.Answers(["November"], "car.txt")));

        Assert.Equal("The tyres are changed every November.", Assert.Single(result.Observation.CitedPassages!));
        Assert.Contains(result.Checks, c => c is CheckResult.Passed { Name: "passage" });
    }

    [Fact]
    public async Task The_cited_passages_are_recorded()
    {
        var file = _app.WriteFile("engine.txt", "Engine noise got louder in September. The tyres are changed every November.");
        var runner = new EvaluationRunner(_app.Library, _app.Pipeline, _app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(
            new EvaluationCase("engine", "engine noise louder", null, new Expectation.Answers(["louder"], "engine.txt")));

        var passage = Assert.Single(result.Observation.CitedPassages!);
        Assert.Contains("louder", passage);
    }

    [Fact]
    public async Task The_retrieval_trace_of_the_question_is_recorded()
    {
        var file = _app.WriteFile("engine.txt", "Engine noise got louder in September.");
        var runner = new EvaluationRunner(_app.Library, _app.Pipeline, _app.Answers);
        await runner.IndexAsync([file]);

        var result = await runner.RunAsync(
            new EvaluationCase("engine", "engine noise louder", null, new Expectation.Answers(["louder"], "engine.txt")));

        var trace = result.Observation.Trace!;
        Assert.Equal("engine noise louder", trace.Query);
        Assert.Contains(trace.Candidates, c => c is { DocumentTitle: "engine.txt", Fate: CandidateFate.Sent });
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

    private static RetrievalTrace Trace(params RetrievalCandidate[] candidates) =>
        new("the search query", new RetrievalOptions(MinScore: 0.35), candidates);

    private static CaseResult Traced(string id, Expectation expect, bool passed, RetrievalTrace trace)
    {
        var testCase = new EvaluationCase(id, "q " + id, null, expect);
        var observation = new AnswerObservation("answer of " + id, [], passed ? [] : ["x.md"], TimeSpan.FromSeconds(1), Trace: trace);
        return new CaseResult(testCase, observation, AnswerScorer.Score(testCase, observation));
    }

    private static EvaluationRun Run(params CaseResult[] results) =>
        new("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1, results);

    [Fact]
    public void The_cases_table_shows_the_best_candidate_score_of_each_case()
    {
        var trace = Trace(
            new RetrievalCandidate("a.md", "Intro", 0.6124, CandidateSource.Meaning, CandidateFate.Sent),
            new RetrievalCandidate("b.md", "", 0.41, CandidateSource.Words, CandidateFate.OutsideMargin));

        var markdown = ReportWriter.Markdown(Run(Traced("scored", new Expectation.Refuses(), true, trace)));

        Assert.Contains("| Case | Result | Best score | Time | Detail |", markdown);
        Assert.Contains("| scored | pass | 0.612 | 1.0 s |", markdown);
    }

    [Fact]
    public void A_case_over_an_empty_archive_has_no_best_score()
    {
        var markdown = ReportWriter.Markdown(Run(Traced("empty", new Expectation.Refuses(), true, Trace())));

        Assert.Contains("| empty | pass | — | 1.0 s |", markdown);
    }

    [Fact]
    public void Failed_cases_list_every_candidate_with_its_score_fate_source_and_section()
    {
        var trace = Trace(
            new RetrievalCandidate("doc.md", "Pricing", 0.612, CandidateSource.Meaning, CandidateFate.Sent),
            new RetrievalCandidate("api.md", "Errors › Codes", 0.48, CandidateSource.MeaningAndWords, CandidateFate.OverBudget),
            new RetrievalCandidate("faq.md", "", 0.3, CandidateSource.Words, CandidateFate.BelowMinScore),
            new RetrievalCandidate("old.md", "Notes", 0.29, CandidateSource.Meaning, CandidateFate.OutsideMargin),
            new RetrievalCandidate("far.md", "Notes", 0.28, CandidateSource.Meaning, CandidateFate.BeyondTopK));

        var markdown = ReportWriter.Markdown(Run(Traced("bad", new Expectation.Refuses(), false, trace)));

        Assert.Contains("Candidates for \"the search query\":", markdown);
        Assert.Contains("- 0.612 sent · meaning · doc.md › Pricing", markdown);
        Assert.Contains("- 0.480 over budget · meaning and words · api.md › Errors › Codes", markdown);
        Assert.Contains("- 0.300 below min score · words · faq.md\n", markdown.ReplaceLineEndings("\n"));
        Assert.Contains("- 0.290 outside margin · meaning · old.md › Notes", markdown);
        Assert.Contains("- 0.280 beyond top k · meaning · far.md › Notes", markdown);
    }

    [Fact]
    public void A_failed_case_over_an_empty_archive_says_there_were_no_candidates()
    {
        var markdown = ReportWriter.Markdown(Run(Traced("bad", new Expectation.Refuses(), false, Trace())));

        Assert.Contains("- none, the archive is empty", markdown);
    }

    [Fact]
    public void Passed_cases_do_not_list_their_candidates()
    {
        var trace = Trace(new RetrievalCandidate("doc.md", "Pricing", 0.612, CandidateSource.Meaning, CandidateFate.Sent));

        var markdown = ReportWriter.Markdown(Run(Traced("ok", new Expectation.Refuses(), true, trace)));

        Assert.DoesNotContain("sent · meaning", markdown);
    }

    [Fact]
    public void The_summary_shows_whether_the_score_floor_separates_answerable_cases_from_refusals()
    {
        static RetrievalTrace Best(double score) =>
            Trace(new RetrievalCandidate("doc.md", "", score, CandidateSource.Meaning, CandidateFate.Sent));
        var answers = new Expectation.Answers([], null);

        var markdown = ReportWriter.Markdown(Run(
            Traced("a1", answers, true, Best(0.71)),
            Traced("a2", answers, true, Best(0.52)),
            Traced("r1", new Expectation.Refuses(), true, Best(0.44)),
            Traced("r2", new Expectation.Refuses(), true, Best(0.31)),
            Traced("r3", new Expectation.Refuses(), true, Trace())));

        Assert.Contains("| Score floor | 0.350 |", markdown);
        Assert.Contains("| Lowest best score, answerable cases | 0.520 |", markdown);
        Assert.Contains("| Highest best score, refusal cases | 0.440 |", markdown);
    }

    [Fact]
    public void A_run_without_traces_has_no_score_lines()
    {
        var markdown = ReportWriter.Markdown(Run(Result("plain", true, 1)));

        Assert.Contains("| plain | pass | — | 1.0 s |", markdown);
        Assert.DoesNotContain("Score floor", markdown);
        Assert.DoesNotContain("best score,", markdown);
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
    public void The_report_names_how_the_documents_were_chunked()
    {
        var run = new EvaluationRun("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1, [Result("ok-case", true, 1)])
        {
            Chunking = new ChunkingOptions(CodeBlocks: CodeBlocks.WithText, SectionPaths: SectionPaths.Omit)
        };

        var markdown = ReportWriter.Markdown(run);

        Assert.Contains("- Chunking: code blocks with text, section paths omitted", markdown);
    }

    [Theory]
    [InlineData(SearchMode.Hybrid, "- Search: meaning and words")]
    [InlineData(SearchMode.Meaning, "- Search: meaning only")]
    public void The_report_names_how_the_archive_was_searched(SearchMode search, string line)
    {
        var run = new EvaluationRun("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1, [Result("ok-case", true, 1)]) { Retrieval = new RetrievalOptions(Search: search) };

        Assert.Contains(line, ReportWriter.Markdown(run));
    }

    [Fact]
    public void The_report_names_the_retrieval_thresholds()
    {
        var run = new EvaluationRun("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1, [Result("ok-case", true, 1)])
        {
            Retrieval = new RetrievalOptions(MinScore: 0.4, ScoreMargin: 0.15)
        };

        var markdown = ReportWriter.Markdown(run);

        Assert.Contains("- Retrieval: min score 0.40, score margin 0.15", markdown);
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

    [Fact]
    public void Failed_cases_list_the_numbered_fragments_that_were_retrieved()
    {
        var testCase = new EvaluationCase("bad", "q", null, new Expectation.Refuses());
        var observation = new AnswerObservation("text", ["x.md"], ["x.md"], TimeSpan.FromSeconds(1),
            RetrievedLines: ["[1] x.md: the preview of the first fragment"]);
        var run = new EvaluationRun("Demo", DateTimeOffset.UnixEpoch, "c", "e", 1,
            [new CaseResult(testCase, observation, AnswerScorer.Score(testCase, observation))]);

        var markdown = ReportWriter.Markdown(run);

        Assert.Contains("[1] x.md: the preview of the first fragment", markdown);
    }

    [Fact]
    public void A_case_can_name_what_the_highlight_must_hold_when_the_answer_words_are_not_in_the_source()
    {
        var cases = CaseLoader.Parse("""
            { "cases": [
              { "id": "a", "question": "Как расторгнуть?", "expect": "answer", "contains": ["недел"], "passage": ["vier Wochen"] }
            ] }
            """);
        var observation = new AnswerObservation("За четыре недели [1].", ["vertrag.md"], ["vertrag.md"], TimeSpan.FromSeconds(1),
            ["Beide Seiten können mit einer Frist von vier Wochen kündigen."]);

        var checks = AnswerScorer.Score(cases[0], observation);

        Assert.Equal(["vier Wochen"], ((Expectation.Answers)cases[0].Expect).Passage);
        Assert.Contains(checks.OfType<CheckResult.Passed>(), p => p.Name == "passage");
    }
}
