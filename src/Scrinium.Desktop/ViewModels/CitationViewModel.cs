using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class CitationViewModel(Citation citation) : ObservableObject
{
    public Citation Source { get; } = citation;

    public int Number { get; } = citation.Number;

    public string Label { get; } = $"[{citation.Number}]";

    public string DocumentTitle { get; } = citation.DocumentTitle;

    public string Subtitle { get; } = citation.Page is { } page
        ? $"p. {page} · {citation.Date:yyyy-MM-dd}"
        : citation.Date.ToString("yyyy-MM-dd");

    public string Preview { get; } = citation.Preview.ReplaceLineEndings(" ");

    public SourceLocation Location { get; } = citation.Location;

    public string Meta => $"{Subtitle} · {(IsCited ? "cited" : "retrieved, not cited")}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Meta), nameof(IsUncited))]
    private bool _isCited;

    public bool IsUncited => !IsCited;

    [ObservableProperty]
    private bool _isActive;
}
