using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;

namespace Scrinium.Extraction;

/// <summary>Reads an HTML page as Markdown: headings, paragraphs, lists, tables and code, without markup or page chrome.</summary>
public sealed partial class HtmlTextExtractor : ITextExtractor
{
    private static readonly HashSet<string> Dropped =
    [
        "head", "script", "style", "noscript", "template", "svg", "canvas", "iframe",
        "form", "button", "nav", "aside", "footer"
    ];

    private static readonly HashSet<string> Blocks =
    [
        "p", "div", "section", "article", "main", "h1", "h2", "h3", "h4", "h5", "h6", "pre",
        "ul", "ol", "table", "blockquote", "figure", "figcaption", "dl", "dt", "dd", "hr", "address", "details", "summary"
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\blanguage-([\w+#-]+)")]
    private static partial Regex CodeLanguage();

    public bool Supports(DocumentKind kind) => kind is DocumentKind.Html;

    public IReadOnlyList<TextSegment> Extract(string path)
    {
        var page = new HtmlDocument();
        page.Load(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var root = page.DocumentNode.SelectSingleNode("//main")
            ?? page.DocumentNode.SelectSingleNode("//article")
            ?? page.DocumentNode.SelectSingleNode("//body")
            ?? page.DocumentNode;

        var blocks = RenderBlocks(root).ToList();
        if (!blocks.Any(b => b.StartsWith("# ", StringComparison.Ordinal)) && Title(page) is { Length: > 0 } title)
            blocks.Insert(0, "# " + title);

        return [new TextSegment(null, string.Join("\n\n", blocks))];
    }

    private static string? Title(HtmlDocument page) =>
        page.DocumentNode.SelectSingleNode("//title") is { } title ? Collapse(Decode(title.InnerText)) : null;

    private static IEnumerable<string> RenderBlocks(HtmlNode container)
    {
        var inline = new StringBuilder();
        foreach (var child in container.ChildNodes)
        {
            if (IsDropped(child))
                continue;

            if (!IsBlock(child))
            {
                AppendInline(inline, child);
                continue;
            }

            if (Paragraph(inline) is { } pending)
                yield return pending;
            inline.Clear();

            foreach (var block in RenderBlock(child))
                yield return block;
        }

        if (Paragraph(inline) is { } last)
            yield return last;
    }

    private static IEnumerable<string> RenderBlock(HtmlNode node) => node.Name switch
    {
        "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => Heading(node),
        "p" or "dt" or "dd" or "figcaption" or "summary" or "address" => Paragraph(Inline(node)) is { } text ? [text] : [],
        "pre" => [Code(node)],
        "ul" or "ol" => List(node, 0) is { Length: > 0 } list ? [list] : [],
        "table" => Table(node) is { Length: > 0 } table ? [table] : [],
        "blockquote" => RenderBlocks(node).Select(b => string.Join("\n", b.Split('\n').Select(line => "> " + line))),
        "hr" => [],
        _ => RenderBlocks(node)
    };

    private static IEnumerable<string> Heading(HtmlNode node)
    {
        var text = Collapse(Inline(node));
        return text.Length == 0 ? [] : [new string('#', node.Name[1] - '0') + " " + text];
    }

    private static string Code(HtmlNode pre)
    {
        var language = CodeLanguage().Match(pre.GetAttributeValue("class", "") + " " +
            (pre.SelectSingleNode("code")?.GetAttributeValue("class", "") ?? "")).Groups[1].Value;
        var body = Decode(pre.InnerText).Replace("\r\n", "\n").Trim('\n');
        return $"```{language}\n{body}\n```";
    }

    private static string List(HtmlNode list, int depth)
    {
        var lines = new List<string>();
        var number = 1;
        foreach (var item in list.ChildNodes.Where(n => n.Name == "li" && !IsDropped(n)))
        {
            var marker = list.Name == "ol" ? $"{number++}. " : "- ";
            var own = new StringBuilder();
            foreach (var child in item.ChildNodes.Where(c => c.Name is not ("ul" or "ol") && !IsDropped(c)))
                AppendInline(own, child);

            lines.Add(new string(' ', depth * 2) + marker + Collapse(own.ToString()));
            lines.AddRange(item.ChildNodes.Where(c => c.Name is "ul" or "ol").Select(nested => List(nested, depth + 1)));
        }

        return string.Join("\n", lines.Where(line => line.Length > 0));
    }

    private static string Table(HtmlNode table) =>
        MarkdownTable.Render(table.Descendants("tr")
            .Select(row => (IReadOnlyList<string>)row.ChildNodes.Where(c => c.Name is "th" or "td").Select(cell => Collapse(Inline(cell))).ToList())
            .ToList());

    private static string Inline(HtmlNode node)
    {
        var builder = new StringBuilder();
        foreach (var child in node.ChildNodes.Where(c => !IsDropped(c)))
            AppendInline(builder, child);
        return builder.ToString();
    }

    private static void AppendInline(StringBuilder builder, HtmlNode node)
    {
        switch (node)
        {
            case HtmlTextNode text:
                builder.Append(Whitespace().Replace(Decode(text.Text), " "));
                break;
            case { Name: "br" }:
                builder.Append('\n');
                break;
            case { Name: "code" or "kbd" or "samp" }:
                builder.Append('`').Append(Collapse(Decode(node.InnerText))).Append('`');
                break;
            case { NodeType: HtmlNodeType.Element } when IsBlock(node):
                builder.Append(' ').Append(string.Join("\n", RenderBlock(node))).Append(' ');
                break;
            case { NodeType: HtmlNodeType.Element }:
                builder.Append(Inline(node));
                break;
        }
    }

    private static string? Paragraph(StringBuilder inline) => Paragraph(inline.ToString());

    private static string? Paragraph(string inline)
    {
        var lines = inline.Split('\n').Select(Collapse).ToList();
        var text = string.Join("\n", lines).Trim('\n');
        return text.Length == 0 ? null : text;
    }

    private static bool IsBlock(HtmlNode node) => node.NodeType == HtmlNodeType.Element && Blocks.Contains(node.Name);

    private static bool IsDropped(HtmlNode node) =>
        node.NodeType == HtmlNodeType.Comment
        || (node.NodeType == HtmlNodeType.Element
            && (Dropped.Contains(node.Name)
                || node.Attributes.Contains("hidden")
                || node.GetAttributeValue("aria-hidden", "") == "true"));

    private static string Decode(string text) => HtmlEntity.DeEntitize(text).Replace(' ', ' ');

    private static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();
}
