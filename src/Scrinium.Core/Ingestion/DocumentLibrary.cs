using System.Collections.Concurrent;
using System.Threading.Channels;
using Scrinium.Core.Documents;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Ingestion;

public sealed class DocumentLibrary(
    IDocumentStore store,
    IngestionPipeline pipeline,
    DataPaths paths,
    TimeProvider clock)
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public event Action<Guid>? DocumentChanged;

    /// <summary>Raised when processing a document failed outside the pipeline's own error handling; the worker goes on.</summary>
    public event Action<Guid, Exception>? ProcessingFailed;

    public Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default) => store.ListAsync(ct);

    public async Task<Document> ImportAsync(string sourcePath, CancellationToken ct = default)
    {
        var kind = DocumentKinds.FromPath(sourcePath)
            ?? throw new NotSupportedException($"Unsupported format: {Path.GetExtension(sourcePath)}");

        var id = Guid.NewGuid();
        var fileDate = DateOnly.FromDateTime(File.GetLastWriteTime(sourcePath));
        var storedPath = paths.NewStoredPath(id, Path.GetExtension(sourcePath), fileDate);
        var destination = paths.Resolve(storedPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var source = File.OpenRead(sourcePath))
        await using (var target = File.Create(destination))
            await source.CopyToAsync(target, ct);

        var document = new Document(
            id,
            Path.GetFileName(sourcePath),
            storedPath,
            kind,
            fileDate,
            clock.GetUtcNow(),
            new IndexState.Pending());

        await store.AddAsync(document, ct);
        Enqueue(id);
        return document;
    }

    public async Task RetryAsync(Guid documentId, CancellationToken ct = default)
    {
        await store.SetStateAsync(documentId, new IndexState.Pending(), ct);
        Enqueue(documentId);
    }

    public async Task ReindexAllAsync(CancellationToken ct = default)
    {
        foreach (var document in await store.ListAsync(ct))
            await RetryAsync(document.Id, ct);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await store.GetAsync(documentId, ct);
        if (document is null)
            return;

        await store.DeleteAsync(documentId, ct);

        var original = paths.Resolve(document.StoredPath);
        if (File.Exists(original))
            File.Delete(original);

        DocumentChanged?.Invoke(documentId);
    }

    public async Task RunWorkerAsync(CancellationToken ct)
    {
        foreach (var document in await store.ListAsync(ct))
        {
            if (document.State is IndexState.Pending or IndexState.Indexing)
                Enqueue(document.Id);
        }

        await foreach (var id in _queue.Reader.ReadAllAsync(ct))
        {
            _queued.TryRemove(id, out _);
            DocumentChanged?.Invoke(id);
            try
            {
                await pipeline.ProcessAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                ProcessingFailed?.Invoke(id, ex);
            }
            finally
            {
                DocumentChanged?.Invoke(id);
            }
        }
    }

    private void Enqueue(Guid documentId)
    {
        // Import and the startup scan can both see a new document; indexing it twice would hide it from search meanwhile.
        if (!_queued.TryAdd(documentId, 0))
            return;

        _queue.Writer.TryWrite(documentId);
        DocumentChanged?.Invoke(documentId);
    }
}
