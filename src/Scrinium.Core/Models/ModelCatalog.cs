namespace Scrinium.Core.Models;

public abstract record ModelCatalog
{
    private ModelCatalog() { }

    public sealed record Available(IReadOnlyList<InstalledModel> Models) : ModelCatalog;

    public sealed record Unavailable : ModelCatalog;
}
