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
        var forbids = element.TryGetProperty("forbids", out var forbidden)
            ? forbidden.EnumerateArray().Select(item => item.GetString()!).ToList()
            : (List<string>)[];

        Expectation expectation = Text(element, "expect") switch
        {
            "refusal" => new Expectation.Refuses(),
            "answer" when contains.Count > 0 || cites is not null => new Expectation.Answers(contains, cites, forbids)
            {
                Passage = element.TryGetProperty("passage", out var passage)
                    ? passage.EnumerateArray().Select(item => item.GetString()!).ToList()
                    : null
            },
            "answer" => throw new InvalidDataException($"The case '{id}' expects an answer but names no fact and no source."),
            var other => throw new InvalidDataException($"The case '{id}' has an unknown expectation '{other}'.")
        };

        return new EvaluationCase(id, question, Text(element, "language"), expectation) { History = History(id, element) };
    }

    private static IReadOnlyList<HistoryTurn> History(string id, JsonElement element)
    {
        if (!element.TryGetProperty("history", out var items))
            return [];

        var turns = new List<HistoryTurn>();
        foreach (var item in items.EnumerateArray())
        {
            var role = Text(item, "role");
            var text = Text(item, "text");
            if (role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"The case '{id}' has a history turn with role '{role}' or no text.");

            turns.Add(new HistoryTurn(role, text));
        }

        return turns;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
