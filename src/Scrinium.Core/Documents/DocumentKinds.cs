namespace Scrinium.Core.Documents;

public static class DocumentKinds
{
    public const string UnknownFormat = "unsupported format (txt, md, pdf, html and docx only for now)";

    public const string WordBinaryFormat =
        "Word 97-2003 documents (.doc) cannot be read. Save it as .docx in Word and add it again.";

    public static FileSupport Check(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => new FileSupport.Supported(DocumentKind.Text),
            ".md" or ".markdown" => new FileSupport.Supported(DocumentKind.Markdown),
            ".pdf" => new FileSupport.Supported(DocumentKind.Pdf),
            ".html" or ".htm" => new FileSupport.Supported(DocumentKind.Html),
            ".docx" => new FileSupport.Supported(DocumentKind.Docx),
            ".doc" => new FileSupport.Unsupported(WordBinaryFormat),
            _ => new FileSupport.Unsupported(UnknownFormat)
        };

    public static bool IsSupported(string path) => Check(path) is FileSupport.Supported;
}
