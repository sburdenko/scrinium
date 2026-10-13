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
    public const string Usage = """
        Usage: Scrinium.Eval --documents <folder> --cases <file.json> [options]

          --report <file>            write a Markdown report
          --chat-model <name>        override the answer model
          --embedding-model <name>   override the embedding model
          --ollama-url <url>         override the Ollama address
          --min-pass <0..1>          exit with code 1 when fewer cases pass
          --only <id,id,...>         run only the listed case ids
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
            ? double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)
            : null;

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
                : null);
    }
}
