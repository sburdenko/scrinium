using System.Text.Json;

namespace Scrinium.Eval;

public static class CaseLoader
{
    public static IReadOnlyList<EvaluationCase> Load(string path) => Parse(File.ReadAllText(path));

    public static IReadOnlyList<EvaluationCase> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("cases").EnumerateArray().Select(ParseCase).ToList();

        var duplicate = items.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"The case id '{duplicate.Key}' is used more than once.");

        return items;
    }

    private static EvaluationCase ParseCase(JsonElement element)
    {
        var id = Text(element, "id") ?? throw new InvalidDataException("A case has no id.");
        var question = Text(element, "question");
        if (string.IsNullOrWhiteSpace(question))
            throw new InvalidDataException($"The case '{id}' has no question.");

        var contains = element.TryGetProperty("contains", out var list)
            ? list.EnumerateArray().Select(item => item.GetString()!).ToList()
            : [];
        var cites = Text(element, "cites");

        Expectation expectation = Text(element, "expect") switch
        {
            "refusal" => new Expectation.Refuses(),
            "answer" when contains.Count > 0 || cites is not null => new Expectation.Answers(contains, cites),
            "answer" => throw new InvalidDataException($"The case '{id}' expects an answer but names no fact and no source."),
            var other => throw new InvalidDataException($"The case '{id}' has an unknown expectation '{other}'.")
        };

        return new EvaluationCase(id, question, Text(element, "language"), expectation);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
