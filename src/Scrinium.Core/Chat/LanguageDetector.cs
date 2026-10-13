namespace Scrinium.Core.Chat;

public static class LanguageDetector
{
    public const int EnoughWords = 7;

    public const string Unknown = "unknown";

    private static readonly Dictionary<string, HashSet<string>> Stopwords = new()
    {
        ["de"] = Set("der die das und ist nicht wir sie ein eine für mit auf bei zu im am den dem von werden ihre ihr haben wird sind oder nur was wie wann welche kann gibt es ich wo wer warum sich auch noch"),
        ["en"] = Set("the and is are we you your for with on at to of in by this that not can will have has our it or only what does do how when which where who why my there their from an a be"),
        ["fr"] = Set("le la les un une des du de et est sont pour avec dans que qui par sur au aux ce cette il elle nous vous ne pas ou où quel quelle comment quand sont ses leurs plus"),
        ["es"] = Set("el la los las un una y es son para con en que por del al se no su sus como cuando cual donde quien más pero también muy está están")
    };

    private static HashSet<string> Set(string words) =>
        new(words.Split(' '), StringComparer.OrdinalIgnoreCase);

    public static int WordCount(string text) => Words(text).Length;

    public static string Detect(string text)
    {
        var letters = text.Count(char.IsLetter);
        if (letters == 0)
            return Unknown;

        var cyrillic = text.Count(c => c is >= 'Ѐ' and <= 'ӿ');
        if (cyrillic * 3 >= letters)
            return "ru";

        var words = Words(text);
        var scores = Stopwords
            .Select(language => (Language: language.Key, Hits: words.Count(language.Value.Contains)))
            .OrderByDescending(score => score.Hits)
            .ToList();

        var best = scores[0];
        var runnerUp = scores[1];
        return best.Hits > 0 && best.Hits > runnerUp.Hits ? best.Language : Unknown;
    }

    private static string[] Words(string text) =>
        text.Split([' ', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\'', '’'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Any(char.IsLetter))
            .ToArray();
}
