namespace Scrinium.Core.Preview;

public sealed record DocumentPreview(
    string Title,
    int? Page,
    string Before,
    string Highlight,
    string After,
    PreviewTruncation Truncation,
    string OriginalPath);
