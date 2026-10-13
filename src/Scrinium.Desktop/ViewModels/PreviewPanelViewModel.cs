using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Preview;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PreviewPanelViewModel(
    DocumentPreview? preview,
    PreviewRequest request,
    Func<CitationViewModel, Task> switchTo) : RightPanelViewModel
{
    public string Title { get; } = preview?.Title ?? request.Title;

    public int? Number { get; } = request.Number;

    public IReadOnlyList<CitationViewModel> Citations { get; } = request.Citations;

    public bool HasSwitcher => Citations.Count > 1;

    public string Kicker { get; } = request.Number is { } number ? $"ORIGINAL · [{number}]" : "ORIGINAL";

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

    [RelayCommand]
    private Task SwitchTo(CitationViewModel? citation) => citation is null ? Task.CompletedTask : switchTo(citation);

    private static string Ellipsis(DocumentPreview? preview, PreviewTruncation edge) =>
        preview is not null && preview.Truncation.HasFlag(edge) ? (edge == PreviewTruncation.Start ? "…\n" : "\n…") : "";
}
