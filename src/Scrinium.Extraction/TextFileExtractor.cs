using System.Text;
using Scrinium.Core.Documents;

using Scrinium.Core.Extraction;

namespace Scrinium.Extraction;

public sealed class TextFileExtractor : ITextExtractor
{
    public bool Supports(DocumentKind kind) => kind is DocumentKind.Text or DocumentKind.Markdown;

    public IReadOnlyList<TextSegment> Extract(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
        return [new TextSegment(null, text)];
    }
}
