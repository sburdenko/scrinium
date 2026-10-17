using Scrinium.Core.Documents;
using Scrinium.Extraction;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class HtmlTextExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Extract(string body, string head = "") =>
        Assert.Single(new HtmlTextExtractor().Extract(_temp.File("page.html", $"<html><head>{head}</head><body>{body}</body></html>"))).Text;

    [Fact]
    public void It_reads_html_documents_only()
    {
        Assert.True(new HtmlTextExtractor().Supports(DocumentKind.Html));
        Assert.False(new HtmlTextExtractor().Supports(DocumentKind.Docx));
    }

    [Fact]
    public void Headings_become_markdown_headings_and_paragraphs_are_separated()
    {
        var text = Extract("<h1>Guide</h1><p>First   paragraph\n spans lines.</p><h2>Install</h2><p>Second.</p>");

        Assert.Equal("# Guide\n\nFirst paragraph spans lines.\n\n## Install\n\nSecond.", text);
    }

    [Fact]
    public void Scripts_styles_navigation_and_footers_are_dropped()
    {
        var text = Extract("""
            <nav><a href="/">Home</a> <a href="/prices">Prices</a></nav>
            <style>.price { color: red }</style>
            <h1>Prices</h1><p>The Team plan costs 49 euros.</p>
            <script>var price = 999;</script>
            <footer>Old phone +49 30 000000</footer>
            """);

        Assert.Equal("# Prices\n\nThe Team plan costs 49 euros.", text);
    }

    [Fact]
    public void The_main_element_is_preferred_over_the_page_around_it()
    {
        var text = Extract("<header><p>Brightmoor Labs</p></header><main><h1>Guide</h1><p>Body.</p></main><div>Cookie banner</div>");

        Assert.Equal("# Guide\n\nBody.", text);
    }

    [Fact]
    public void Hidden_elements_are_dropped()
    {
        var text = Extract("<h1>Guide</h1><p hidden>Secret draft.</p><div aria-hidden=\"true\">Icon</div><p>Visible.</p>");

        Assert.Equal("# Guide\n\nVisible.", text);
    }

    [Fact]
    public void Lists_keep_their_items_whole_and_nest()
    {
        var text = Extract("<ul><li>Clone the repository</li><li>Install:<ol><li>Node 22</li><li>pnpm</li></ol></li></ul>");

        Assert.Equal("- Clone the repository\n- Install:\n  1. Node 22\n  2. pnpm", text);
    }

    [Fact]
    public void Tables_become_markdown_tables()
    {
        var text = Extract("<table><tr><th>Plan</th><th>Price</th></tr><tr><td>Team</td><td>49 | 59</td></tr></table>");

        Assert.Equal("| Plan | Price |\n| --- | --- |\n| Team | 49 / 59 |", text);
    }

    [Fact]
    public void Preformatted_code_becomes_a_fenced_block_that_keeps_its_whitespace()
    {
        var text = Extract("<p>Run:</p><pre><code class=\"language-bash\">brightmoor sync \\\n    --since &lt;date&gt;\n</code></pre>");

        Assert.Equal("Run:\n\n```bash\nbrightmoor sync \\\n    --since <date>\n```", text);
    }

    [Fact]
    public void Inline_code_and_entities_are_kept_readable()
    {
        var text = Extract("<p>Set <code>BRIGHTMOOR_TOKEN</code> &amp; restart&nbsp;the agent.</p>");

        Assert.Equal("Set `BRIGHTMOOR_TOKEN` & restart the agent.", text);
    }

    [Fact]
    public void A_page_without_a_top_heading_is_titled_by_its_title_element()
    {
        var text = Extract("<h2>Install</h2><p>Body.</p>", "<title>Brightmoor Guide</title>");

        Assert.Equal("# Brightmoor Guide\n\n## Install\n\nBody.", text);
    }

    [Fact]
    public void Line_breaks_inside_a_paragraph_are_kept()
    {
        var text = Extract("<p>Brightmoor Labs<br>Hafenstraße 4<br/>Hamburg</p>");

        Assert.Equal("Brightmoor Labs\nHafenstraße 4\nHamburg", text);
    }
}
