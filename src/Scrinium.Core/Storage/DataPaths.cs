namespace Scrinium.Core.Storage;

public sealed class DataPaths(string root)
{
    public string Root { get; } = root;

    public string DatabasePath => Path.Combine(Root, "scrinium.db");

    public string OriginalsDirectory => Path.Combine(Root, "originals");

    public string NewStoredPath(Guid id, string extension, DateOnly date) =>
        Path.Combine("originals", date.Year.ToString("0000"), date.Month.ToString("00"), id.ToString("N") + extension.ToLowerInvariant());

    public string Resolve(string storedPath) => Path.Combine(Root, storedPath);
}
