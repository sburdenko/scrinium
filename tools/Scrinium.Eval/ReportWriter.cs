using System.Globalization;
using System.Text;
using Scrinium.Core.Chunking;
using Scrinium.Core.Retrieval;

namespace Scrinium.Eval;

public sealed record EvaluationRun(
    string Name,
    DateTimeOffset At,
    string ChatModel,
    string EmbeddingModel,
    int DocumentCount,
    IReadOnlyList<CaseResult> Results)
{
    public ChunkingOptions Chunking { get; init; } = new();

    public RetrievalOptions Retrieval { get; init; } = new();
}

public static class ReportWriter
{
    public static string Markdown(EvaluationRun run)
    {
        var summary = EvaluationSummary.From(run.Results);
        var report = new StringBuilder();

        report.AppendLine($"# Evaluation: {run.Name}").AppendLine();
        report.AppendLine($"- Date: {run.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine($"- Chat model: `{run.ChatModel}`");
        report.AppendLine($"- Embedding model: `{run.EmbeddingModel}`");
        report.AppendLine($"- Documents: {run.DocumentCount}");
        report.AppendLine($"- Chunking: {Describe(run.Chunking)}");
        report.AppendLine($"- Search: {(run.Retrieval.Search == SearchMode.Hybrid ? "meaning and words" : "meaning only")}");
        report.AppendLine($"- Retrieval: {Describe(run.Retrieval)}");
        report.AppendLine().AppendLine("## Summary").AppendLine();
        report.AppendLine("| Measure | Result |").AppendLine("|---|---|");
        report.AppendLine($"| Cases passed | {Ratio(summary.Passed, summary.Total)} |");
        report.AppendLine($"| Expected facts present | {Ratio(summary.FactsCorrect, summary.Answerable)} |");
        report.AppendLine($"| Right document cited | {Ratio(summary.SourcesCorrect, summary.SourcesChecked)} |");
        if (summary.PassagesChecked > 0)
            report.AppendLine($"| Highlighted sentence holds the fact | {Ratio(summary.PassagesCorrect, summary.PassagesChecked)} |");
        report.AppendLine($"| Refused when the documents do not say | {Ratio(summary.RefusalsCorrect, summary.Refusals)} |");
        report.AppendLine($"| Answered in the language of the question | {Ratio(summary.LanguageCorrect, summary.LanguageChecked)} |");
        report.AppendLine($"| Median time per answer | {summary.MedianTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s |");
        report.AppendLine($"| Slowest answer | {summary.SlowestTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s |");
        AppendScoreSeparation(report, run.Results);

        report.AppendLine().AppendLine("## Cases").AppendLine();
        report.AppendLine("| Case | Result | Best score | Time | Detail |").AppendLine("|---|---|---|---|---|");
        foreach (var result in run.Results)
        {
            var failures = result.Checks.OfType<CheckResult.Failed>().Select(f => $"{f.Name}: {f.Reason}");
            report.AppendLine(
                $"| {result.Case.Id} | {(result.Passed ? "pass" : "FAIL")} | {TraceFormat.Score(result.Observation.BestScore)} | " +
                $"{result.Observation.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s | " +
                $"{Escape(string.Join("; ", failures))} |");
        }

        var failed = run.Results.Where(r => !r.Passed).ToList();
        if (failed.Count > 0)
        {
            report.AppendLine().AppendLine("## Failed cases").AppendLine();
            foreach (var result in failed)
            {
                report.AppendLine($"### {result.Case.Id}").AppendLine();
                report.AppendLine($"Question: {result.Case.Question}").AppendLine();
                report.AppendLine($"Answer: {Shorten(result.Observation.Text)}").AppendLine();
                if (result.Observation.CitedPassages is { Count: > 0 } highlighted)
                {
                    report.AppendLine("Highlighted:").AppendLine();
                    foreach (var passage in highlighted)
                        report.AppendLine($"- {passage.ReplaceLineEndings(" ")}");
                    report.AppendLine();
                }

                report.AppendLine("Retrieved (first pass):").AppendLine();
                foreach (var line in result.Observation.RetrievedLines ?? result.Observation.RetrievedTitles)
                    report.AppendLine($"- {line}");
                report.AppendLine();

                if (result.Observation.Trace is { } trace)
                    AppendCandidates(report, trace);
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// The score floor next to the weakest answerable case and the strongest refusal: the floor separates them when it
    /// lies between the two.
    /// </summary>
    private static void AppendScoreSeparation(StringBuilder report, IReadOnlyList<CaseResult> results)
    {
        var floor = results.Select(r => r.Observation.Trace).OfType<RetrievalTrace>().FirstOrDefault()?.Options.MinScore;
        if (floor is null)
            return;

        var answerable = BestScores(results, r => r.Case.Expect is Expectation.Answers);
        var refusals = BestScores(results, r => r.Case.Expect is Expectation.Refuses);
        report.AppendLine($"| Score floor | {TraceFormat.Score(floor)} |");
        if (answerable.Count > 0)
            report.AppendLine($"| Lowest best score, answerable cases | {TraceFormat.Score(answerable.Min())} |");
        if (refusals.Count > 0)
            report.AppendLine($"| Highest best score, refusal cases | {TraceFormat.Score(refusals.Max())} |");
    }

    private static List<double> BestScores(IReadOnlyList<CaseResult> results, Func<CaseResult, bool> include) =>
        results.Where(include).Select(r => r.Observation.BestScore).OfType<double>().ToList();

    private static void AppendCandidates(StringBuilder report, RetrievalTrace trace)
    {
        report.AppendLine($"Candidates for \"{trace.Query.ReplaceLineEndings(" ")}\":").AppendLine();
        if (trace.Candidates.Count == 0)
            report.AppendLine("- none, the archive is empty");
        foreach (var candidate in trace.Candidates)
            report.AppendLine(TraceFormat.Line(candidate));
        report.AppendLine();
    }

    private static string Shorten(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 600 ? flat : flat[..600] + "…";
    }

    private static string Ratio(int part, int whole) =>
        whole == 0 ? "n/a" : $"{part} / {whole} ({100.0 * part / whole:0} %)".Replace(',', '.');

    private static string Escape(string text) => text.Replace("|", "\\|").ReplaceLineEndings(" ");

    private static string Describe(ChunkingOptions chunking)
    {
        var code = chunking.CodeBlocks == CodeBlocks.OwnChunks ? "code blocks in their own chunks" : "code blocks with text";
        var sections = chunking.SectionPaths == SectionPaths.Prepend ? "section paths prepended" : "section paths omitted";
        return $"{code}, {sections}";
    }

    private static string Describe(RetrievalOptions retrieval) => string.Create(
        CultureInfo.InvariantCulture, $"min score {retrieval.MinScore:0.00}, score margin {retrieval.ScoreMargin:0.00}");
}
