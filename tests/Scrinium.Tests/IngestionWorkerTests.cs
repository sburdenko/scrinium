using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Storage;
using Scrinium.Storage;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class IngestionWorkerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task A_storage_failure_on_one_document_does_not_stop_the_worker()
    {
        var paths = new DataPaths(Path.Combine(_temp.Path, "data"));
        var store = new FlakyStore(new SqliteStore(paths.DatabasePath));
        var chunker = new Scrinium.Core.Chunking.TextChunker(new Scrinium.Core.Chunking.ChunkingOptions(MaxTokens: 60, OverlapTokens: 8));
        var settings = new Scrinium.Core.Settings.SettingsService(Path.Combine(_temp.Path, "settings.json"));
        var pipeline = new IngestionPipeline(store, [new Scrinium.Extraction.TextFileExtractor()], chunker, new FakeEmbeddingGenerator(), settings, paths);
        var library = new DocumentLibrary(store, pipeline, paths, TimeProvider.System);
        var failures = new List<Guid>();
        library.ProcessingFailed += (id, _) => failures.Add(id);
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(() => library.RunWorkerAsync(stop.Token));

        store.FailNextStateChange = true;
        var broken = await library.ImportAsync(_temp.File("broken.txt", "This one hits a locked database."));
        var fine = await library.ImportAsync(_temp.File("fine.txt", "This one should still be indexed."));

        for (var attempt = 0; attempt < 300 && (await library.ListAsync()).Single(d => d.Id == fine.Id).State is not IndexState.Ready; attempt++)
            await Task.Delay(10);

        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.IsType<IndexState.Ready>((await library.ListAsync()).Single(d => d.Id == fine.Id).State);
        Assert.Contains(broken.Id, failures);
    }

    private sealed class FlakyStore(SqliteStore inner) : IDocumentStore
    {
        public bool FailNextStateChange { get; set; }

        public Task AddAsync(Document document, CancellationToken ct = default) => inner.AddAsync(document, ct);

        public Task SetStateAsync(Guid documentId, IndexState state, CancellationToken ct = default)
        {
            if (!FailNextStateChange)
                return inner.SetStateAsync(documentId, state, ct);

            FailNextStateChange = false;
            throw new IOException("database is locked");
        }

        public Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);

        public Task<Document?> GetAsync(Guid documentId, CancellationToken ct = default) => inner.GetAsync(documentId, ct);

        public Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<EmbeddedChunk> chunks, string embeddingModel, CancellationToken ct = default) =>
            inner.ReplaceChunksAsync(documentId, chunks, embeddingModel, ct);

        public Task<IReadOnlyList<Chunk>> GetChunkRangeAsync(Guid documentId, int fromOrdinal, int toOrdinal, CancellationToken ct = default) =>
            inner.GetChunkRangeAsync(documentId, fromOrdinal, toOrdinal, ct);

        public Task DeleteAsync(Guid documentId, CancellationToken ct = default) => inner.DeleteAsync(documentId, ct);
    }

    [Fact]
    public async Task A_document_queued_by_import_and_by_the_startup_scan_is_indexed_once()
    {
        var paths = new DataPaths(Path.Combine(_temp.Path, "data"));
        var store = new SqliteStore(paths.DatabasePath);
        var chunker = new Scrinium.Core.Chunking.TextChunker(new Scrinium.Core.Chunking.ChunkingOptions(MaxTokens: 60, OverlapTokens: 8));
        var settings = new Scrinium.Core.Settings.SettingsService(Path.Combine(_temp.Path, "settings.json"));
        var embedder = new FakeEmbeddingGenerator();
        var pipeline = new IngestionPipeline(store, [new Scrinium.Extraction.TextFileExtractor()], chunker, embedder, settings, paths);
        var library = new DocumentLibrary(store, pipeline, paths, TimeProvider.System);

        var document = await library.ImportAsync(_temp.File("note.txt", "Engine noise got louder in September."));
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(() => library.RunWorkerAsync(stop.Token));

        for (var attempt = 0; attempt < 300 && (await library.ListAsync()).Single(d => d.Id == document.Id).State is not IndexState.Ready; attempt++)
            await Task.Delay(10);
        await Task.Delay(200);

        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.Equal(1, embedder.Calls);
    }
}
