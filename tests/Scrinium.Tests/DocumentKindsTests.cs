using Scrinium.Core.Documents;

namespace Scrinium.Tests;

public class DocumentKindsTests
{
    [Theory]
    [InlineData("notes.txt", DocumentKind.Text)]
    [InlineData("guide.md", DocumentKind.Markdown)]
    [InlineData("scan.pdf", DocumentKind.Pdf)]
    [InlineData("page.html", DocumentKind.Html)]
    [InlineData("page.htm", DocumentKind.Html)]
    [InlineData("contract.docx", DocumentKind.Docx)]
    [InlineData("CONTRACT.DOCX", DocumentKind.Docx)]
    public void Supported_files_are_recognised_by_extension(string name, DocumentKind kind) =>
        Assert.Equal(kind, Assert.IsType<FileSupport.Supported>(DocumentKinds.Check(name)).Kind);

    [Fact]
    public void A_word_97_document_is_refused_with_a_way_out()
    {
        var refused = Assert.IsType<FileSupport.Unsupported>(DocumentKinds.Check("old-contract.doc"));

        Assert.Contains("Save it as .docx", refused.Reason);
        Assert.False(DocumentKinds.IsSupported("old-contract.doc"));
    }

    [Fact]
    public void An_unknown_format_lists_the_formats_that_work()
    {
        var refused = Assert.IsType<FileSupport.Unsupported>(DocumentKinds.Check("photo.jpg"));

        Assert.Equal("unsupported format (txt, md, pdf, html and docx only for now)", refused.Reason);
    }
}
