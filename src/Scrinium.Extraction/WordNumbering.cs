using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Scrinium.Extraction;

/// <summary>Turns Word list numbering into Markdown markers, counting each list level as Word does.</summary>
internal sealed class WordNumbering
{
    private const int IndentPerLevel = 2;

    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> _bulletsByList;
    private readonly Dictionary<(int List, int Level), int> _counters = [];

    private WordNumbering(IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> bulletsByList) =>
        _bulletsByList = bulletsByList;

    public static WordNumbering From(MainDocumentPart main)
    {
        var numbering = main.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
            return new WordNumbering(new Dictionary<int, IReadOnlyDictionary<int, bool>>());

        var abstracts = numbering.Elements<AbstractNum>()
            .Where(a => a.AbstractNumberId?.Value is not null)
            .GroupBy(a => a.AbstractNumberId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, bool>)g.First().Elements<Level>()
                    .Where(l => l.LevelIndex?.Value is not null)
                    .GroupBy(l => l.LevelIndex!.Value)
                    .ToDictionary(lg => lg.Key, lg => lg.First().NumberingFormat?.Val?.Value == NumberFormatValues.Bullet));

        var lists = numbering.Elements<NumberingInstance>()
            .Where(n => n.NumberID?.Value is not null && n.AbstractNumId?.Val?.Value is { } id && abstracts.ContainsKey(id))
            .GroupBy(n => n.NumberID!.Value)
            .ToDictionary(g => g.Key, g => abstracts[g.First().AbstractNumId!.Val!.Value]);

        return new WordNumbering(lists);
    }

    /// <summary>The Markdown marker for a list paragraph, or null when the paragraph is not in a list.</summary>
    public string? Marker(Paragraph paragraph, WordStyles styles)
    {
        if (styles.Numbering(paragraph) is not { } properties || properties.NumberingId?.Val?.Value is not { } list || list == 0)
            return null;

        var level = properties.NumberingLevelReference?.Val?.Value ?? 0;
        var indent = new string(' ', level * IndentPerLevel);
        var bullet = !_bulletsByList.TryGetValue(list, out var levels) || !levels.TryGetValue(level, out var isBullet) || isBullet;
        if (bullet)
            return indent + "- ";

        foreach (var deeper in _counters.Keys.Where(k => k.List == list && k.Level > level).ToList())
            _counters.Remove(deeper);

        var number = _counters.GetValueOrDefault((list, level)) + 1;
        _counters[(list, level)] = number;
        return $"{indent}{number}. ";
    }
}
