using System.Globalization;
using System.Text;

namespace Scrinium.Eval;

public sealed record EvaluationRun(
    string Name,
    DateTimeOffset At,
    string ChatModel,
    string EmbeddingModel,
    int DocumentCount,
    IReadOnlyList<CaseResult> Results);

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
        report.AppendLine().AppendLine("## Summary").AppendLine();
        report.AppendLine("| Measure | Result |").AppendLine("|---|---|");
        report.AppendLine($"| Cases passed | {Ratio(summary.Passed, summary.Total)} |");
        report.AppendLine($"| Expected facts present | {Ratio(summary.FactsCorrect, summary.Answerable)} |");
        report.AppendLine($"| Right document cited | {Ratio(summary.SourcesCorrect, summary.SourcesChecked)} |");
        report.AppendLine($"| Refused when the documents do not say | {Ratio(summary.RefusalsCorrect, summary.Refusals)} |");
        report.AppendLine($"| Answered in the language of the question | {Ratio(summary.LanguageCorrect, summary.LanguageChecked)} |");
        report.AppendLine($"| Median time per answer | {summary.MedianTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s |");
        report.AppendLine($"| Slowest answer | {summary.SlowestTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s |");

        report.AppendLine().AppendLine("## Cases").AppendLine();
        report.AppendLine("| Case | Result | Time | Detail |").AppendLine("|---|---|---|---|");
        foreach (var result in run.Results)
        {
            var failures = result.Checks.OfType<CheckResult.Failed>().Select(f => $"{f.Name}: {f.Reason}");
            report.AppendLine(
                $"| {result.Case.Id} | {(result.Passed ? "pass" : "FAIL")} | " +
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
                report.AppendLine($"Retrieved: {string.Join(", ", result.Observation.RetrievedTitles)}").AppendLine();
            }
        }

        return report.ToString();
    }

    private static string Shorten(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 600 ? flat : flat[..600] + "…";
    }

    private static string Ratio(int part, int whole) =>
        whole == 0 ? "n/a" : $"{part} / {whole} ({100.0 * part / whole:0} %)".Replace(',', '.');

    private static string Escape(string text) => text.Replace("|", "\\|").ReplaceLineEndings(" ");
}
