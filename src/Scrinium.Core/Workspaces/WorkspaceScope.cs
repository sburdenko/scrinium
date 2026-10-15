using Scrinium.Core.Chat;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Sessions;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Workspaces;

/// <summary>Everything that works on one workspace's documents. Dispose it to stop its indexing worker.</summary>
public sealed class WorkspaceScope(
    Workspace workspace,
    DataPaths paths,
    DocumentLibrary library,
    IngestionPipeline pipeline,
    Retriever retriever,
    AnswerService answers,
    DocumentPreviewService previews,
    ISessionStore sessions) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task _worker = Task.CompletedTask;
    private int _disposed;

    public Workspace Workspace { get; } = workspace;

    public DataPaths Paths { get; } = paths;

    public DocumentLibrary Library { get; } = library;

    public IngestionPipeline Pipeline { get; } = pipeline;

    public Retriever Retriever { get; } = retriever;

    public AnswerService Answers { get; } = answers;

    public DocumentPreviewService Previews { get; } = previews;

    public ISessionStore Sessions { get; } = sessions;

    /// <summary>Starts indexing queued documents on the thread pool; does nothing if the worker already runs.</summary>
    public void StartWorker()
    {
        if (_worker.IsCompleted)
            _worker = Task.Run(() => Library.RunWorkerAsync(_stop.Token));
    }

    /// <summary>Asks the worker to stop without waiting for it, for callers that must not block.</summary>
    public void StopWorker()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _stop.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await _stop.CancelAsync();
        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
    }
}
