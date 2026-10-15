using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class PassageLocatorTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task A_heading_is_not_chosen_as_the_first_highlight()
    {
        const string text = "## Kann ich Unterlagen digital einreichen?\n\nJa. Nutzen Sie das Mandantenportal oder senden Sie die Dateien verschlüsselt per E-Mail.";
        var chunk = new Chunk(Guid.NewGuid(), Guid.NewGuid(), 0, null, 0, text, new DateOnly(2026, 1, 1), 20);
        var question = FakeEmbeddingGenerator.Embed("Kann ich Unterlagen digital einreichen?");

        var located = await new PassageLocator(_app.Embedder, _app.Settings).LocateAsync([new ChunkHit(chunk, "faq.md", 0.9)], question, default);

        var span = located[chunk.Id];
        Assert.StartsWith("Ja. Nutzen Sie", text.Substring(span.Start, span.Length));
    }
}
