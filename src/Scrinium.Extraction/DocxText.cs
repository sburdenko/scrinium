using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Scrinium.Extraction;

internal static class DocxText
{
    public static string Of(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        Append(builder, paragraph);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, OpenXmlElement element)
    {
        foreach (var child in element.ChildElements)
        {
            switch (child)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;
                case TabChar:
                    builder.Append('\t');
                    break;
                case Break or CarriageReturn:
                    builder.Append('\n');
                    break;
                case NoBreakHyphen:
                    builder.Append('-');
                    break;
                case AlternateContentFallback or DeletedRun:
                    break;
                default:
                    Append(builder, child);
                    break;
            }
        }
    }
}
