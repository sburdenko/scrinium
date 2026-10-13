using Scrinium.Core.Documents;

namespace Scrinium.Desktop.ViewModels;

public sealed class SourceItemViewModel(Document document, string currentEmbeddingModel)
{
    public Guid Id { get; } = document.Id;

    public string Title { get; } = document.Title;

    public string KindLabel { get; } = document.Kind switch
    {
        DocumentKind.Pdf => "PDF",
        DocumentKind.Markdown => "MD",
        _ => "TXT"
    };

    public string DateText { get; } = document.FileDate.ToString("yyyy-MM-dd");

    public SourceStatus Status { get; } = document.State switch
    {
        IndexState.Pending => SourceStatus.Waiting,
        IndexState.Indexing => SourceStatus.Indexing,
        IndexState.Ready => SourceStatus.Ready,
        _ => SourceStatus.Failed
    };

    public bool IsStale { get; } =
        document.State is IndexState.Ready ready && ready.EmbeddingModel != currentEmbeddingModel;

    public string StatusText { get; } = document.State switch
    {
        IndexState.Pending => "queued",
        IndexState.Indexing => "indexing…",
        IndexState.Ready ready when ready.EmbeddingModel != currentEmbeddingModel => "needs reindexing",
        IndexState.Ready ready => ready.ChunkCount == 1 ? "1 chunk" : $"{ready.ChunkCount} chunks",
        IndexState.Failed failed => failed.Error,
        _ => ""
    };

    public string StateLabel { get; } = document.State switch
    {
        IndexState.Pending => "Queued",
        IndexState.Indexing => "Indexing",
        IndexState.Ready ready when ready.EmbeddingModel != currentEmbeddingModel => "Needs reindex",
        IndexState.Ready => "Ready",
        _ => "Failed"
    };

    public bool CanRetry => IsFailed;

    public bool IsFailed => Status == SourceStatus.Failed;

    public bool IsBusy => Status is SourceStatus.Waiting or SourceStatus.Indexing;

    public bool IsReady => Status == SourceStatus.Ready && !IsStale;
}
