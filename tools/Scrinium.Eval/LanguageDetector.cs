namespace Scrinium.Eval;

public static class LanguageDetector
{
    private static readonly HashSet<string> German = new(StringComparer.OrdinalIgnoreCase)
    {
        "der", "die", "das", "und", "ist", "nicht", "wir", "sie", "ein", "eine", "für", "mit", "auf", "bei",
        "zu", "im", "am", "den", "dem", "von", "werden", "ihre", "ihr", "haben", "wird", "sind", "oder", "nur"
    };

    private static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "is", "are", "we", "you", "your", "for", "with", "on", "at", "to", "of", "in", "by",
        "this", "that", "not", "can", "will", "have", "has", "our", "it", "or", "only"
    };

    public static string Detect(string text)
    {
        var letters = text.Count(char.IsLetter);
        if (letters == 0)
            return "unknown";

        var cyrillic = text.Count(c => c is >= 'Ѐ' and <= 'ӿ');
        if (cyrillic * 3 >= letters)
            return "ru";

        var words = text
            .Split([' ', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\''], StringSplitOptions.RemoveEmptyEntries);
        var german = words.Count(German.Contains);
        var english = words.Count(English.Contains);

        return german > english ? "de" : english > german ? "en" : "unknown";
    }
}
