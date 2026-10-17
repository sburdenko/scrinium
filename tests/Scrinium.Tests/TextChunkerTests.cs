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

    private static string Section(string topic) =>
        string.Join(" ", Enumerable.Range(1, 30).Select(i => $"Строка {i} про {topic}."));

    private static readonly TextChunker WithoutOverlap = new(new ChunkingOptions(MaxTokens: 100, OverlapTokens: 0));

    [Fact]
    public void Text_without_headings_has_no_section()
    {
        var chunks = _chunker.Chunk([new TextSegment(null, Sentences(40))]);

        Assert.All(chunks, c => Assert.Equal("", c.Section));
    }

    [Fact]
    public void Later_chunks_of_a_long_section_carry_its_heading()
    {
        var chunks = WithoutOverlap.Chunk([new TextSegment(null, $"# Тарифы\n{Section("тарифы")}")]);

        Assert.True(chunks.Count > 2);
        Assert.StartsWith("# Тарифы", chunks[0].Text, StringComparison.Ordinal);
        Assert.All(chunks.Skip(1), c => Assert.Equal("Тарифы", c.Section));
    }

    [Fact]
    public void A_chunk_that_starts_with_its_heading_does_not_repeat_it_as_its_section()
    {
        var chunks = WithoutOverlap.Chunk([new TextSegment(null, $"# Тарифы\n{Section("тарифы")}")]);

        Assert.Equal("", chunks[0].Section);
    }

    [Fact]
    public void Nested_headings_form_a_path()
    {
        var text = $"# Тарифы\nВводный абзац.\n## Бизнес\n{Section("бизнес")}";

        var chunks = WithoutOverlap.Chunk([new TextSegment(null, text)]);

        Assert.Equal("Тарифы › Бизнес", chunks[^1].Section);
    }

    [Fact]
    public void A_heading_replaces_the_previous_one_of_the_same_or_deeper_level()
    {
        var text = $"# Тарифы\n## Бизнес\n### Скидки\n{Section("скидки")}\n## Частный\n{Section("частный")}";

        var chunks = WithoutOverlap.Chunk([new TextSegment(null, text)]);

        Assert.Equal("Тарифы › Частный", chunks[^1].Section);
    }

    [Fact]
    public void Hash_lines_inside_code_fences_are_not_headings()
    {
        var text = $"# Установка\n```bash\n# install the tool\nscrinium --help\n```\n{Section("установку")}";

        var chunks = WithoutOverlap.Chunk([new TextSegment(null, text)]);

        Assert.Equal("Установка", chunks[^1].Section);
    }

    [Fact]
    public void Headings_trim_closing_hashes()
    {
        var chunks = WithoutOverlap.Chunk([new TextSegment(null, $"## Тарифы ##\n{Section("тарифы")}")]);

        Assert.Equal("Тарифы", chunks[^1].Section);
    }

    [Fact]
    public void A_chunk_whose_overlap_reaches_back_into_the_previous_section_is_labelled_by_its_own_section()
    {
        var overlapping = new TextChunker(new ChunkingOptions(MaxTokens: 100, OverlapTokens: 30));
        var text = $"# Тарифы\n## Бизнес\n{Section("бизнес")}\n## Частный\n{Section("частный")}";

        var chunks = overlapping.Chunk([new TextSegment(null, text)]);

        var first = Assert.Single(chunks, c => c.Text.Contains("## Частный", StringComparison.Ordinal) && !c.Text.StartsWith("## Частный", StringComparison.Ordinal));
        Assert.Equal("Тарифы", first.Section);
        Assert.All(chunks.SkipWhile(c => c != first).Skip(1), c => Assert.Equal("Тарифы › Частный", c.Section));
    }

    [Fact]
    public void Overlap_starts_at_the_beginning_of_a_sentence_when_one_is_close()
    {
        var chunks = new TextChunker(new ChunkingOptions(MaxTokens: 100, OverlapTokens: 30)).Chunk([new TextSegment(null, Sentences(40))]);

        Assert.All(chunks.Skip(1), c => Assert.StartsWith("Предложение номер", c.Text, StringComparison.Ordinal));
        Assert.All(chunks.Zip(chunks.Skip(1)), pair => Assert.True(pair.Second.Offset < pair.First.Offset + pair.First.Text.Length));
    }

    [Fact]
    public void Embedding_text_puts_the_section_before_the_chunk_text()
    {
        var withSection = new ChunkDraft(null, 0, "Цена 10 евро.", 4, "Тарифы › Бизнес");
        var withoutSection = new ChunkDraft(null, 0, "Цена 10 евро.", 4, "");

        Assert.Equal("Тарифы › Бизнес\n\nЦена 10 евро.", withSection.EmbeddingText);
        Assert.Equal("Цена 10 евро.", withoutSection.EmbeddingText);
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
