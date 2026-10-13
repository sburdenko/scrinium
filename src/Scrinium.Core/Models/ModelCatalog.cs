namespace Scrinium.Core.Models;

public sealed record InstalledModel(string Name, bool CanChat, bool CanEmbed);

public abstract record ModelCatalog
{
    private ModelCatalog() { }

    public sealed record Available(IReadOnlyList<InstalledModel> Models) : ModelCatalog;

    public sealed record Unavailable : ModelCatalog;
}
