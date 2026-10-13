namespace Scrinium.Core.Models;

public sealed record ModelChoices(IReadOnlyList<string> Chat, IReadOnlyList<string> Embedding)
{
    public static ModelChoices From(ModelCatalog catalog, string currentChat, string currentEmbedding)
    {
        var installed = catalog is ModelCatalog.Available available ? available.Models : [];

        return new ModelChoices(
            WithCurrent(currentChat, installed.Where(m => m.CanChat).Select(m => m.Name)),
            WithCurrent(currentEmbedding, installed.Where(m => m.CanEmbed).Select(m => m.Name)));
    }

    private static IReadOnlyList<string> WithCurrent(string current, IEnumerable<string> names) =>
        names
            .Where(name => !SameModel(name, current))
            .Order()
            .Prepend(current)
            .ToList();

    private static bool SameModel(string a, string b) =>
        Untagged(a).Equals(Untagged(b), StringComparison.OrdinalIgnoreCase);

    private static string Untagged(string name) =>
        name.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ? name[..^":latest".Length] : name;
}
