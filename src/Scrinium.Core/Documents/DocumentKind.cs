namespace Scrinium.Core.Documents;

public enum DocumentKind
{
    Text,
    Markdown,
    Pdf
}

public static class DocumentKinds
{
    public static DocumentKind? FromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => DocumentKind.Text,
            ".md" or ".markdown" => DocumentKind.Markdown,
            ".pdf" => DocumentKind.Pdf,
            _ => null
        };

    public static bool IsSupported(string path) => FromPath(path) is not null;
}
