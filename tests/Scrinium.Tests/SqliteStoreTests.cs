using Scrinium.Storage;
using Scrinium.Core.Documents;
using Scrinium.Core.Storage;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class SqliteStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteStore _store;

    public SqliteStoreTests() => _store = new SqliteStore(Path.Combine(_temp.Path, "test.db"));

    public void Dispose() => _temp.Dispose();

    private static Document NewDocument(string title = "note.md") => new(
        Guid.NewGuid(), title, $"originals/{title}", DocumentKind.Markdown,
        new DateOnly(2026, 9, 14), DateTimeOffset.UtcNow, new IndexState.Pending());

    private static EmbeddedChunk NewChunk(Document doc, int ordinal, string text, params float[] vector) =>
        new(new Chunk(Guid.NewGuid(), doc.Id, ordinal, null, 0, text, doc.FileDate, 10), vector);

    [Fact]
    public async Task Added_document_is_listed_as_pending()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);

        var listed = Assert.Single(await _store.ListAsync());

        Assert.Equal(doc.Id, listed.Id);
        Assert.IsType<IndexState.Pending>(listed.State);
        Assert.Equal(new DateOnly(2026, 9, 14), listed.FileDate);
    }

    [Fact]
    public async Task Failed_state_keeps_error_text()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);

        await _store.SetStateAsync(doc.Id, new IndexState.Failed("нет текста"));

        var state = (await _store.GetAsync(doc.Id))!.State;
        Assert.Equal("нет текста", Assert.IsType<IndexState.Failed>(state).Error);
    }

    [Fact]
    public async Task Replacing_chunks_marks_document_ready_with_model()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);

        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "a", 1, 0)], "bge-m3");

        var ready = Assert.IsType<IndexState.Ready>((await _store.GetAsync(doc.Id))!.State);
        Assert.Equal("bge-m3", ready.EmbeddingModel);
        Assert.Equal(1, ready.ChunkCount);
    }

    [Fact]
    public async Task Search_returns_nearest_chunks_first()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id,
            [NewChunk(doc, 0, "far", 0, 1), NewChunk(doc, 1, "near", 1, 0.1f), NewChunk(doc, 2, "mid", 1, 1)], "m");

        var hits = await _store.SearchAsync([1, 0], "m", 2);

        Assert.Equal(["near", "mid"], hits.Select(h => h.Chunk.Text));
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public async Task Search_ignores_documents_indexed_with_another_model()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "a", 1, 0)], "old-model");

        Assert.Empty(await _store.SearchAsync([1, 0], "new-model", 5));
    }

    [Fact]
    public async Task Search_ignores_vectors_of_other_dimensions()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "a", 1, 0, 0)], "m");

        Assert.Empty(await _store.SearchAsync([1, 0], "m", 5));
    }

    [Fact]
    public async Task Chunk_range_returns_neighbours_in_order()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id,
            Enumerable.Range(0, 5).Select(i => NewChunk(doc, i, $"c{i}", 1, 0)).ToList(), "m");

        var range = await _store.GetChunkRangeAsync(doc.Id, 1, 3);

        Assert.Equal(["c1", "c2", "c3"], range.Select(c => c.Text));
    }

    [Fact]
    public async Task Deleting_document_removes_its_chunks()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "a", 1, 0)], "m");

        await _store.DeleteAsync(doc.Id);

        Assert.Empty(await _store.ListAsync());
        Assert.Empty(await _store.SearchAsync([1, 0], "m", 5));
    }

    [Fact]
    public async Task Replacing_chunks_twice_keeps_only_the_latest_set()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "old", 1, 0)], "m");
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "new", 1, 0)], "m");

        var hits = await _store.SearchAsync([1, 0], "m", 5);

        Assert.Equal("new", Assert.Single(hits).Chunk.Text);
    }

    [Fact]
    public async Task Chunk_section_and_content_survive_storage()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        var chunk = new Chunk(Guid.NewGuid(), doc.Id, 0, null, 0, "```\nmake\n```", doc.FileDate, 4, "Тарифы › Бизнес", ChunkContent.Code);
        await _store.ReplaceChunksAsync(doc.Id, [new EmbeddedChunk(chunk, [1, 0])], "m");

        var ranged = Assert.Single(await _store.GetChunkRangeAsync(doc.Id, 0, 0));
        var found = Assert.Single(await _store.SearchAsync([1, 0], "m", 5)).Chunk;
        Assert.Equal(("Тарифы › Бизнес", ChunkContent.Code), (ranged.Section, ranged.Content));
        Assert.Equal(("Тарифы › Бизнес", ChunkContent.Code), (found.Section, found.Content));
    }

    [Fact]
    public async Task A_database_from_before_sections_keeps_its_chunks_with_no_section()
    {
        var path = Path.Combine(_temp.Path, "old.db");
        var doc = NewDocument();
        var old = new SqliteStore(path);
        await old.AddAsync(doc);
        await old.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "kept", 1, 0)], "m");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var downgrade = connection.CreateCommand();
            downgrade.CommandText = "DROP TABLE chunk_words; ALTER TABLE chunks DROP COLUMN section; ALTER TABLE chunks DROP COLUMN content; PRAGMA user_version = 2;";
            downgrade.ExecuteNonQuery();
        }

        var hit = Assert.Single(await new SqliteStore(path).SearchAsync([1, 0], "m", 5));

        Assert.Equal("kept", hit.Chunk.Text);
        Assert.Equal("", hit.Chunk.Section);
        Assert.Equal(ChunkContent.Prose, hit.Chunk.Content);
    }

    private async Task<Document> ReadyDocumentAsync(string model, params string[] chunkTexts)
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        await _store.ReplaceChunksAsync(doc.Id, chunkTexts.Select((text, i) => NewChunk(doc, i, text, 1, 0)).ToList(), model);
        return doc;
    }

    [Fact]
    public async Task The_word_index_finds_a_chunk_by_an_identifier_it_contains()
    {
        await ReadyDocumentAsync("m", "| E507 | The destination disk is full |", "| E401 | The API key has expired |", "Plain text about disks.");

        var matches = await _store.FindAsync(["E507"], "m", 5);

        Assert.Equal("| E507 | The destination disk is full |", Assert.Single(matches).Chunk.Text);
        Assert.Equal([1f, 0f], matches[0].Embedding);
    }

    [Fact]
    public async Task The_word_index_matches_an_identifier_with_punctuation_as_a_phrase()
    {
        await ReadyDocumentAsync("m", "Send the X-Brightmoor-Key header.", "The key goes to Brightmoor in a header called X.");

        var matches = await _store.FindAsync(["X-Brightmoor-Key"], "m", 5);

        Assert.Equal("Send the X-Brightmoor-Key header.", Assert.Single(matches).Chunk.Text);
    }

    [Fact]
    public async Task The_word_index_ranks_chunks_with_more_of_the_terms_first()
    {
        await ReadyDocumentAsync("m", "P1 once among several other words here", "P1 and again P1 here", "P4 can wait.");

        var matches = await _store.FindAsync(["P1"], "m", 5);

        Assert.Equal(2, matches.Count);
        Assert.Equal("P1 and again P1 here", matches[0].Chunk.Text);
    }

    [Fact]
    public async Task The_word_index_only_returns_ready_documents_of_the_current_model()
    {
        await ReadyDocumentAsync("old-model", "E507 under another model.");
        var pending = NewDocument("pending.md");
        await _store.AddAsync(pending);

        Assert.Empty(await _store.FindAsync(["E507"], "m", 5));
    }

    [Fact]
    public async Task The_word_index_follows_reindexing_and_deletion()
    {
        var doc = await ReadyDocumentAsync("m", "E507 old text.");
        await _store.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "E401 new text.", 1, 0)], "m");

        Assert.Empty(await _store.FindAsync(["E507"], "m", 5));
        Assert.Single(await _store.FindAsync(["E401"], "m", 5));

        await _store.DeleteAsync(doc.Id);

        Assert.Empty(await _store.FindAsync(["E401"], "m", 5));
    }

    [Theory]
    [InlineData("say \"hi\" now")]
    [InlineData("NOT")]
    [InlineData("a*b OR c")]
    [InlineData("(E507")]
    public async Task Terms_that_look_like_query_syntax_are_searched_as_plain_text(string term)
    {
        await ReadyDocumentAsync("m", "E507 is a disk error.");

        var matches = await _store.FindAsync([term], "m", 5);

        Assert.True(matches.Count <= 1);
    }

    [Fact]
    public async Task No_terms_find_nothing()
    {
        await ReadyDocumentAsync("m", "E507 is a disk error.");

        Assert.Empty(await _store.FindAsync([], "m", 5));
    }

    [Fact]
    public async Task A_database_from_before_the_word_index_gets_its_chunks_indexed()
    {
        var path = Path.Combine(_temp.Path, "before-words.db");
        var doc = NewDocument();
        var old = new SqliteStore(path);
        await old.AddAsync(doc);
        await old.ReplaceChunksAsync(doc.Id, [NewChunk(doc, 0, "E507 is a disk error.", 1, 0)], "m");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var downgrade = connection.CreateCommand();
            downgrade.CommandText = "DROP TABLE chunk_words; PRAGMA user_version = 3;";
            downgrade.ExecuteNonQuery();
        }

        var matches = await new SqliteStore(path).FindAsync(["E507"], "m", 5);

        Assert.Equal("E507 is a disk error.", Assert.Single(matches).Chunk.Text);
    }
}
