using Scrinium.Core.Documents;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class PreviewTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Preview_highlights_the_cited_range()
    {
        var text = "Начало документа. " + new string('x', 3000) + " ВАЖНЫЙ КУСОК " + new string('y', 3000);
        var document = await _app.ImportAndProcessAsync(_app.WriteFile("long.txt", text));
        var offset = text.IndexOf("ВАЖНЫЙ", StringComparison.Ordinal);

        var preview = await _app.Preview.LoadAsync(new SourceLocation(document.Id, null, offset, "ВАЖНЫЙ КУСОК".Length));

        Assert.NotNull(preview);
        Assert.Equal("ВАЖНЫЙ КУСОК", preview.Highlight);
        Assert.True(preview.HasMoreBefore);
        Assert.True(preview.HasMoreAfter);
        Assert.EndsWith("x ", preview.Before);
    }

    [Fact]
    public async Task Preview_of_missing_document_is_null()
    {
        Assert.Null(await _app.Preview.LoadAsync(SourceLocation.Start(Guid.NewGuid())));
    }

    [Fact]
    public async Task Pdf_preview_shows_the_requested_page()
    {
        var document = await _app.ImportAndProcessAsync(
            _app.WriteFile("d.pdf", TestPdf.WithPages("First page text.", "Second page text.")));

        var preview = await _app.Preview.LoadAsync(new SourceLocation(document.Id, 2, 0, 6));

        Assert.Equal(2, preview!.Page);
        Assert.Equal("Second", preview.Highlight);
    }
}
