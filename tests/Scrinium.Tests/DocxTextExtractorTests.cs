using DocumentFormat.OpenXml.Wordprocessing;
using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;
using Scrinium.Extraction;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class DocxTextExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Extract(TestDocx docx)
    {
        var path = System.IO.Path.Combine(_temp.Path, "document.docx");
        File.WriteAllBytes(path, docx.Build());
        return Assert.Single(new DocxTextExtractor().Extract(path)).Text;
    }

    [Fact]
    public void It_reads_word_documents_only()
    {
        Assert.True(new DocxTextExtractor().Supports(DocumentKind.Docx));
        Assert.False(new DocxTextExtractor().Supports(DocumentKind.Html));
    }

    [Fact]
    public void Heading_styles_become_markdown_headings()
    {
        var text = Extract(new TestDocx()
            .Heading(0, "Handbook")
            .Heading(1, "Travel")
            .Paragraph("Book trains first.")
            .Heading(2, "Hotels")
            .Paragraph("Up to 140 euros a night."));

        Assert.Equal("# Handbook\n\n# Travel\n\nBook trains first.\n\n## Hotels\n\nUp to 140 euros a night.", text);
    }

    [Fact]
    public void A_heading_style_is_found_by_its_name_whatever_its_id()
    {
        var text = Extract(new TestDocx()
            .Style("berschrift2", "heading 2")
            .Styled("berschrift2", "Reisen")
            .Paragraph("Bahn zuerst."));

        Assert.Equal("## Reisen\n\nBahn zuerst.", text);
    }

    [Fact]
    public void A_custom_style_with_an_outline_level_is_a_heading()
    {
        var text = Extract(new TestDocx()
            .Style("Chapter", "Chapter", outlineLevel: 1)
            .Styled("Chapter", "On-call")
            .Paragraph("One week in six."));

        Assert.Equal("## On-call\n\nOne week in six.", text);
    }

    [Fact]
    public void Bullets_and_numbered_items_become_one_list()
    {
        var text = Extract(new TestDocx()
            .Paragraph("Before the first day:")
            .Numbered("Sign the contract")
            .Numbered("Send your IBAN")
            .Bullet("Laptop", level: 1)
            .Numbered("Read the handbook"));

        Assert.Equal("Before the first day:\n\n1. Sign the contract\n2. Send your IBAN\n  - Laptop\n3. Read the handbook", text);
    }

    [Fact]
    public void Tables_become_markdown_tables()
    {
        var text = Extract(new TestDocx().Table(["Priority", "Response"], ["P1", "1 hour | 24/7"]));

        Assert.Equal("| Priority | Response |\n| --- | --- |\n| P1 | 1 hour / 24/7 |", text);
    }

    [Fact]
    public void Code_styled_paragraphs_become_one_fenced_block()
    {
        var text = Extract(new TestDocx()
            .Paragraph("Clone the repository:")
            .Code("git clone git@git.brightmoor.dev:core/agent.git")
            .Code("cd agent")
            .Paragraph("Then build it."));

        Assert.Equal("Clone the repository:\n\n```\ngit clone git@git.brightmoor.dev:core/agent.git\ncd agent\n```\n\nThen build it.", text);
    }

    [Fact]
    public void Paragraphs_set_in_a_monospace_font_are_code()
    {
        var text = Extract(new TestDocx()
            .Monospace("export BRIGHTMOOR_ENV=staging")
            .Monospace("make run", font: "Courier New"));

        Assert.Equal("```\nexport BRIGHTMOOR_ENV=staging\nmake run\n```", text);
    }

    [Fact]
    public void Tabs_and_line_breaks_inside_a_paragraph_are_kept()
    {
        var text = Extract(new TestDocx().Element(new Paragraph(new Run(
            new Text("Brightmoor Labs"), new Break(), new Text("Hafenstraße 4"), new TabChar(), new Text("Hamburg")))));

        Assert.Equal("Brightmoor Labs\nHafenstraße 4\tHamburg", text);
    }

    [Fact]
    public void Deleted_revisions_are_not_read()
    {
        var text = Extract(new TestDocx().Element(new Paragraph(
            new Run(new Text("Notice period: ")),
            new DeletedRun(new Run(new DeletedText("4 weeks"))),
            new InsertedRun(new Run(new Text("3 months"))))));

        Assert.Equal("Notice period: 3 months", text);
    }

    [Fact]
    public void A_file_that_is_not_a_word_document_fails_with_a_readable_error()
    {
        var path = _temp.File("broken.docx", "this is not a zip file");

        var error = Assert.Throws<TextExtractionException>(() => new DocxTextExtractor().Extract(path));

        Assert.Contains("not a readable Word document", error.Message);
    }
}
