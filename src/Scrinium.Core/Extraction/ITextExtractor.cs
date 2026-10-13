using Scrinium.Core.Documents;

namespace Scrinium.Core.Extraction;

public interface ITextExtractor
{
    bool Supports(DocumentKind kind);

    IReadOnlyList<TextSegment> Extract(string path);
}
