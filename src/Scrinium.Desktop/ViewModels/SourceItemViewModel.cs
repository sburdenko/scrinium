using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Documents;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class SourceItemViewModel(Document document, string currentEmbeddingModel, int position) : ObservableObject
{
    public Guid Id { get; } = document.Id;

    public string Title { get; } = document.Title;

    public string NumberText { get; } = (position + 1).ToString("00");

    public string KindLabel { get; } = document.Kind switch
    {
        DocumentKind.Pdf => "PDF",
        DocumentKind.Markdown => "MD",
        DocumentKind.Html => "HTML",
        DocumentKind.Docx => "DOCX",
        _ => "TXT"
    };

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
        IndexState.Pending => "Queued",
        IndexState.Indexing => "Indexing…",
        IndexState.Ready ready when ready.EmbeddingModel != currentEmbeddingModel => "Needs reindexing",
        IndexState.Ready ready => ready.ChunkCount == 1 ? "1 chunk" : $"{ready.ChunkCount} chunks",
        IndexState.Failed failed => failed.Error,
        _ => ""
    };

    public string DetailText => IsReady ? $"{KindLabel} · {StatusText}" : StatusText;

    public bool CanRetry => IsFailed;

    public bool IsFailed => Status == SourceStatus.Failed;

    public bool IsBusy => Status is SourceStatus.Waiting or SourceStatus.Indexing;

    public bool IsReady => Status == SourceStatus.Ready && !IsStale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCited))]
    private string _citationMarks = "";

    public bool IsCited => CitationMarks.Length > 0;

    [ObservableProperty]
    private bool _isOpenInOriginal;
}
