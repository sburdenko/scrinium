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
    public async Task Chunk_section_survives_storage()
    {
        var doc = NewDocument();
        await _store.AddAsync(doc);
        var chunk = new Chunk(Guid.NewGuid(), doc.Id, 0, null, 0, "Цена 10 евро.", doc.FileDate, 4, "Тарифы › Бизнес");
        await _store.ReplaceChunksAsync(doc.Id, [new EmbeddedChunk(chunk, [1, 0])], "m");

        Assert.Equal("Тарифы › Бизнес", Assert.Single(await _store.GetChunkRangeAsync(doc.Id, 0, 0)).Section);
        Assert.Equal("Тарифы › Бизнес", Assert.Single(await _store.SearchAsync([1, 0], "m", 5)).Chunk.Section);
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
            downgrade.CommandText = "ALTER TABLE chunks DROP COLUMN section; PRAGMA user_version = 2;";
            downgrade.ExecuteNonQuery();
        }

        var hit = Assert.Single(await new SqliteStore(path).SearchAsync([1, 0], "m", 5));

        Assert.Equal("kept", hit.Chunk.Text);
        Assert.Equal("", hit.Chunk.Section);
    }
}
