using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Preview;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PreviewPanelViewModel : RightPanelViewModel
{
    private readonly Func<CitationViewModel, Task> _switchTo;
    private readonly string? _originalPath;

    public PreviewPanelViewModel(
        DocumentPreview? preview,
        PreviewRequest? request,
        Func<CitationViewModel, Task> switchTo)
    {
        _switchTo = switchTo;
        _originalPath = preview?.OriginalPath;
        Citations = request?.Citations ?? [];
        Number = request?.Number;
        DocumentId = request?.Location.DocumentId;
        Title = preview?.Title ?? request?.Title ?? "";
        LocationLabel = preview?.Page is { } page ? $"Page {page}" : "";
        Before = Ellipsis(preview, PreviewTruncation.Start) + (preview?.Before ?? "");
        Highlight = preview?.Highlight ?? "";
        After = (preview?.After ?? "") + Ellipsis(preview, PreviewTruncation.End);

        State = (request, preview) switch
        {
            (null, _) => OriginalState.Empty,
            (_, null) => OriginalState.Unavailable,
            ({ Number: null }, _) => OriginalState.Document,
            _ => OriginalState.Passage
        };
    }

    public static PreviewPanelViewModel Empty() => new(null, null, _ => Task.CompletedTask);

    public OriginalState State { get; }

    public bool IsEmpty => State == OriginalState.Empty;

    public bool IsUnavailable => State == OriginalState.Unavailable;

    public bool ShowsText => State is OriginalState.Document or OriginalState.Passage;

    public bool CanOpenFile => ShowsText && _originalPath is not null;

    public Guid? DocumentId { get; }

    public int? Number { get; }

    public IReadOnlyList<CitationViewModel> Citations { get; }

    public bool HasPager => State == OriginalState.Passage && Citations.Count > 1;

    public string Kicker => State == OriginalState.Passage && PositionOf(Number) is { } index
        ? $"ORIGINAL · {index + 1} OF {Citations.Count}"
        : "ORIGINAL";

    public string Title { get; }

    public string LocationLabel { get; }

    public string Before { get; }

    public string Highlight { get; }

    public string After { get; }

    [RelayCommand]
    private void OpenOriginal()
    {
        if (_originalPath is not null)
            ExternalOpener.Open(_originalPath);
    }

    [RelayCommand]
    private Task Previous() => Step(-1);

    [RelayCommand]
    private Task Next() => Step(1);

    private Task Step(int direction)
    {
        if (!HasPager || PositionOf(Number) is not { } index)
            return Task.CompletedTask;

        var target = (index + direction + Citations.Count) % Citations.Count;
        return _switchTo(Citations[target]);
    }

    private int? PositionOf(int? number)
    {
        for (var i = 0; i < Citations.Count; i++)
        {
            if (Citations[i].Number == number)
                return i;
        }

        return null;
    }

    private static string Ellipsis(DocumentPreview? preview, PreviewTruncation edge) =>
        preview is not null && preview.Truncation.HasFlag(edge) ? (edge == PreviewTruncation.Start ? "…\n" : "\n…") : "";
}
