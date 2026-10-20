using System.Globalization;
using Scrinium.Core.Chunking;
using Scrinium.Core.Retrieval;

namespace Scrinium.Eval;

public sealed record EvalOptions(
    string Documents,
    string Cases,
    string? Report,
    string? ChatModel,
    string? EmbeddingModel,
    string? OllamaUrl,
    double? MinPass,
    IReadOnlyList<string>? Only = null)
{
    public ChunkingOptions Chunking { get; init; } = new();

    public RetrievalOptions Retrieval { get; init; } = new();

    public const string Usage = """
        Usage: Scrinium.Eval --documents <folder> --cases <file.json> [options]

          --report <file>            write a Markdown report
          --chat-model <name>        override the answer model
          --embedding-model <name>   override the embedding model
          --ollama-url <url>         override the Ollama address
          --min-pass <0..1>          exit with code 1 when fewer cases pass
          --only <id,id,...>         run only the listed case ids
          --code-blocks <choice>     own (default): code in chunks of its own; with-text: code stays in the text
          --section-paths <choice>   prepend (default): chunks carry their heading path; omit: they do not
          --search <choice>          hybrid (default): meaning and the identifiers a question names; meaning: meaning only
          --min-score <0..1>         drop retrieved chunks scoring below this (default 0.35)
          --score-margin <0..1>      drop retrieved chunks this far below the best one (default 0.10)
        """;

    public static EvalOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                throw new ArgumentException($"Unexpected argument '{args[i]}'.");

            values[args[i]] = args[i + 1];
        }

        string Required(string key) =>
            values.TryGetValue(key, out var value) ? value : throw new ArgumentException($"{key} is required.");

        double? minPass = values.TryGetValue("--min-pass", out var raw)
            ? double.Parse(raw, CultureInfo.InvariantCulture)
            : null;

        var retrieval = new RetrievalOptions();
        return new EvalOptions(
            Required("--documents"),
            Required("--cases"),
            values.GetValueOrDefault("--report"),
            values.GetValueOrDefault("--chat-model"),
            values.GetValueOrDefault("--embedding-model"),
            values.GetValueOrDefault("--ollama-url"),
            minPass,
            values.TryGetValue("--only", out var only)
                ? only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : null)
        {
            Chunking = new ChunkingOptions(
                CodeBlocks: Choice(values, "--code-blocks", new() { ["own"] = CodeBlocks.OwnChunks, ["with-text"] = CodeBlocks.WithText }, CodeBlocks.OwnChunks),
                SectionPaths: Choice(values, "--section-paths", new() { ["prepend"] = SectionPaths.Prepend, ["omit"] = SectionPaths.Omit }, SectionPaths.Prepend)),
            Retrieval = retrieval with
            {
                MinScore = Fraction(values, "--min-score", retrieval.MinScore),
                ScoreMargin = Fraction(values, "--score-margin", retrieval.ScoreMargin),
                Search = Choice(values, "--search", new() { ["hybrid"] = SearchMode.Hybrid, ["meaning"] = SearchMode.Meaning }, retrieval.Search)
            }
        };
    }

    private static T Choice<T>(Dictionary<string, string> values, string key, Dictionary<string, T> choices, T fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        return choices.TryGetValue(raw, out var choice)
            ? choice
            : throw new ArgumentException($"{key} must be one of: {string.Join(", ", choices.Keys)}.");
    }

    private static double Fraction(Dictionary<string, string> values, string key, double fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 1
            ? value
            : throw new ArgumentException($"{key} must be a number from 0 to 1.");
    }
}
