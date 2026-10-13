using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;

namespace Scrinium.Tests;

public class TextChunkerTests
{
    private readonly TextChunker _chunker = new(new ChunkingOptions(MaxTokens: 100, OverlapTokens: 15));

    private static string Sentences(int count) =>
        string.Join(" ", Enumerable.Range(1, count).Select(i => $"Предложение номер {i} рассказывает о звуке мотора."));

    [Fact]
    public void Empty_text_gives_no_chunks() =>
        Assert.Empty(_chunker.Chunk([new TextSegment(null, "  \n ")]));

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        var chunks = _chunker.Chunk([new TextSegment(null, "Короткая заметка.")]);

        var chunk = Assert.Single(chunks);
        Assert.Equal("Короткая заметка.", chunk.Text);
        Assert.Equal(0, chunk.Offset);
    }

    [Fact]
    public void Long_text_is_split_within_budget_plus_overlap()
    {
        var chunks = _chunker.Chunk([new TextSegment(null, Sentences(60))]);

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c => Assert.True(c.TokenCount <= 100 + 15 + 1, $"chunk has {c.TokenCount} tokens"));
    }

    [Fact]
    public void Chunk_offsets_point_to_the_original_text()
    {
        var text = Sentences(40);

        var chunks = _chunker.Chunk([new TextSegment(null, text)]);

        Assert.All(chunks, c => Assert.Equal(c.Text, text.Substring(c.Offset, c.Text.Length)));
    }

    [Fact]
    public void Chunks_cover_the_whole_text()
    {
        var text = Sentences(40);

        var chunks = _chunker.Chunk([new TextSegment(null, text)]);

        Assert.Equal(0, chunks[0].Offset);
        var last = chunks[^1];
        Assert.Equal(text.Length, last.Offset + last.Text.Length);
        for (var i = 1; i < chunks.Count; i++)
            Assert.True(chunks[i].Offset <= chunks[i - 1].Offset + chunks[i - 1].Text.Length);
    }

    [Fact]
    public void Neighbouring_chunks_overlap()
    {
        var chunks = _chunker.Chunk([new TextSegment(null, Sentences(40))]);

        Assert.True(chunks[1].Offset < chunks[0].Offset + chunks[0].Text.Length);
    }

    [Fact]
    public void Markdown_headings_start_new_chunks_when_text_is_long()
    {
        var section = string.Join(" ", Enumerable.Range(1, 18).Select(i => $"Строка {i} про хостинг."));
        var text = $"# Первый\n{section}\n# Второй\n{section}";
        var withoutOverlap = new TextChunker(new ChunkingOptions(MaxTokens: 100, OverlapTokens: 0));

        var chunks = withoutOverlap.Chunk([new TextSegment(null, text)]);

        Assert.Contains(chunks, c => c.Text.StartsWith("# Второй", StringComparison.Ordinal));
        Assert.DoesNotContain(chunks, c => c.Text.Contains("# Первый") && c.Text.Contains("# Второй"));
    }

    [Fact]
    public void Page_number_is_kept_per_segment()
    {
        var chunks = _chunker.Chunk([new TextSegment(1, "Первая страница."), new TextSegment(2, "Вторая страница.")]);

        Assert.Equal([1, 2], chunks.Select(c => c.Page));
    }

    [Fact]
    public void Text_without_separators_is_hard_split()
    {
        var chunks = _chunker.Chunk([new TextSegment(null, new string('x', 2000))]);

        Assert.True(chunks.Count > 1);
        Assert.Equal(2000, chunks.Sum(c => c.Text.Length) - Overlap(chunks));
    }

    private static int Overlap(IReadOnlyList<ChunkDraft> chunks) =>
        chunks.Zip(chunks.Skip(1), (a, b) => Math.Max(0, a.Offset + a.Text.Length - b.Offset)).Sum();
}
