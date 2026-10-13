using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Documents;
using System.Collections.ObjectModel;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Models;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Desktop.Ollama;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PreviewPanelViewModel(DocumentPreview? preview, string fallbackTitle) : RightPanelViewModel
{
    public string Title { get; } = preview?.Title ?? fallbackTitle;

    public string PageLabel { get; } = preview?.Page is { } page ? $"Page {page}" : "";

    public string Before { get; } = Ellipsis(preview, PreviewTruncation.Start) + (preview?.Before ?? "");

    public string Highlight { get; } = preview?.Highlight ?? "";

    public string After { get; } = (preview?.After ?? "") + Ellipsis(preview, PreviewTruncation.End);

    public bool IsAvailable { get; } = preview is not null;

    private string? OriginalPath { get; } = preview?.OriginalPath;

    [RelayCommand]
    private void OpenOriginal()
    {
        if (OriginalPath is not null)
            ExternalOpener.Open(OriginalPath);
    }

    private static string Ellipsis(DocumentPreview? preview, PreviewTruncation edge) =>
        preview is not null && preview.Truncation.HasFlag(edge) ? (edge == PreviewTruncation.Start ? "…\n" : "\n…") : "";
}
