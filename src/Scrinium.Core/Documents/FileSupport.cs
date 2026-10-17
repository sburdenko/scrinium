namespace Scrinium.Core.Documents;

/// <summary>Whether a file can go into the archive, and if not, what to tell the person who added it.</summary>
public abstract record FileSupport
{
    private FileSupport() { }

    public sealed record Supported(DocumentKind Kind) : FileSupport;

    public sealed record Unsupported(string Reason) : FileSupport;
}
