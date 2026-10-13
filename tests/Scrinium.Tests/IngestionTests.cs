using Scrinium.Core.Documents;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class IngestionTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Text_file_becomes_ready_with_embedding_model()
    {
        var path = _app.WriteFile("notes.md", "# Мотор\nЗвук мотора в сентябре стал громче. Нужно проверить подшипник.");

        var document = await _app.ImportAndProcessAsync(path);

        var ready = Assert.IsType<IndexState.Ready>(document.State);
        Assert.Equal(_app.Settings.Current.EmbeddingModel, ready.EmbeddingModel);
        Assert.True(ready.ChunkCount >= 1);
        Assert.Equal(DocumentKind.Markdown, document.Kind);
    }

    [Fact]
    public async Task Import_copies_original_into_data_folder()
    {
        var path = _app.WriteFile("a.txt", "содержимое");

        var document = await _app.Library.ImportAsync(path);

        Assert.True(File.Exists(_app.Paths.Resolve(document.StoredPath)));
        Assert.StartsWith("originals", document.StoredPath);
    }

    [Fact]
    public async Task Import_rejects_unsupported_format()
    {
        var path = _app.WriteFile("a.xyz", "x");

        await Assert.ThrowsAsync<NotSupportedException>(() => _app.Library.ImportAsync(path));
    }

    [Fact]
    public async Task Pdf_pages_are_kept_in_chunk_metadata()
    {
        var path = _app.WriteFile("doc.pdf", TestPdf.WithPages("Page one about hosting.", "Page two about contract cancellation."));

        var document = await _app.ImportAndProcessAsync(path);

        Assert.IsType<IndexState.Ready>(document.State);
        var hits = await _app.Store.SearchAsync(
            Scrinium.Tests.Support.FakeEmbeddingGenerator.Embed("contract cancellation"),
            _app.Settings.Current.EmbeddingModel, 1);
        Assert.Equal(2, hits[0].Chunk.Page);
    }

    [Fact]
    public async Task Pdf_without_text_layer_fails_with_readable_error()
    {
        var path = _app.WriteFile("scan.pdf", TestPdf.WithPages(""));

        var document = await _app.ImportAndProcessAsync(path);

        var failed = Assert.IsType<IndexState.Failed>(document.State);
        Assert.Contains("scan", failed.Error);
    }

    [Fact]
    public async Task Embedding_failure_marks_document_failed_and_retry_recovers()
    {
        var path = _app.WriteFile("a.txt", "Текст для индексации.");
        _app.Embedder.FailWith = new HttpRequestException("connection refused");

        var failedDocument = await _app.ImportAndProcessAsync(path);

        var failed = Assert.IsType<IndexState.Failed>(failedDocument.State);
        Assert.Contains("connection refused", failed.Error);

        _app.Embedder.FailWith = null;
        await _app.Pipeline.ProcessAsync(failedDocument.Id);

        Assert.IsType<IndexState.Ready>((await _app.Store.GetAsync(failedDocument.Id))!.State);
    }

    [Fact]
    public async Task Deleting_document_removes_original_and_index()
    {
        var path = _app.WriteFile("a.txt", "Текст для удаления.");
        var document = await _app.ImportAndProcessAsync(path);

        await _app.Library.DeleteAsync(document.Id);

        Assert.False(File.Exists(_app.Paths.Resolve(document.StoredPath)));
        Assert.Empty(await _app.Library.ListAsync());
    }

    [Fact]
    public async Task Worker_processes_queued_documents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var worker = _app.Library.RunWorkerAsync(cts.Token);

        var document = await _app.Library.ImportAsync(_app.WriteFile("a.txt", "Фоновая индексация."));
        while (await IsPendingAsync(document.Id) && !cts.IsCancellationRequested)
            await Task.Delay(20);

        Assert.IsType<IndexState.Ready>((await _app.Store.GetAsync(document.Id))!.State);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    private async Task<bool> IsPendingAsync(Guid id) =>
        (await _app.Store.GetAsync(id))!.State is IndexState.Pending or IndexState.Indexing;
}
