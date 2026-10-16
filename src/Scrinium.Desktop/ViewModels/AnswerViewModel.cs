using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Chat;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class AnswerViewModel : ObservableObject
{
    public event Action? CitationsChanged;

    public ObservableCollection<CitationViewModel> Citations { get; } = [];

    public IReadOnlyList<CitationViewModel> CitedCitations => Citations.Where(c => c.IsCited).ToList();

    public bool HasCitations => Citations.Count > 0;

    public bool HasCited => Citations.Any(c => c.IsCited);

    public string StatsLine
    {
        get
        {
            var cited = Citations.Count(c => c.IsCited);
            var fragments = Citations.Count == 1 ? "1 fragment retrieved" : $"{Citations.Count} fragments retrieved";
            return $"{fragments} · {cited} cited · answered from your documents only";
        }
    }

    public bool HasStats => IsFinished && HasCitations;

    [ObservableProperty]
    private int? _activeCitationNumber;

    partial void OnActiveCitationNumberChanged(int? value)
    {
        foreach (var citation in Citations)
            citation.IsActive = citation.Number == value;
    }

    public bool IsPending => Phase is MessagePhase.Searching or MessagePhase.Composing;

    public string StatusLine => Phase switch
    {
        MessagePhase.Searching => "Searching your documents…",
        MessagePhase.Composing when Citations.Count == 0 => "No matching fragments found…",
        MessagePhase.Composing => $"Found {Citations.Count} {(Citations.Count == 1 ? "fragment" : "fragments")}. Writing the answer…",
        _ => ""
    };

    public bool IsFailed => Phase == MessagePhase.Failed;

    public bool IsStopped => Phase == MessagePhase.Stopped;

    public bool IsTextVisible => Phase is MessagePhase.Writing or MessagePhase.Done or MessagePhase.Stopped;

    public bool IsFinished => Phase is MessagePhase.Done or MessagePhase.Stopped;

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(StatusLine), nameof(IsFailed), nameof(IsStopped), nameof(IsTextVisible), nameof(IsFinished))]
    private MessagePhase _phase = MessagePhase.Searching;

    [ObservableProperty]
    private string _error = "";

    public void ApplySources(IReadOnlyList<Citation> citations)
    {
        Citations.Clear();
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation));

        OnActiveCitationNumberChanged(ActiveCitationNumber);
        Phase = MessagePhase.Composing;
        OnPropertyChanged(nameof(StatusLine));
        NotifyCitationsChanged();
    }

    public void RefineSources(IReadOnlyList<Citation> citations)
    {
        Citations.Clear();
        var cited = CitationMarkers.CitedNumbers(Text);
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation) { IsCited = cited.Contains(citation.Number) });

        OnActiveCitationNumberChanged(ActiveCitationNumber);
        NotifyCitationsChanged();
    }

    public void Restore(string text, IReadOnlyList<Citation> citations, MessagePhase ending)
    {
        Text = text;
        Citations.Clear();
        var cited = CitationMarkers.CitedNumbers(text);
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation) { IsCited = cited.Contains(citation.Number) });

        Phase = ending;
        NotifyCitationsChanged();
    }

    public void Decline(string explanation)
    {
        Text = explanation;
        Citations.Clear();
        NotifyCitationsChanged();
    }

    public void Append(string delta)
    {
        Text += delta;
        Phase = MessagePhase.Writing;
    }

    public void Complete() => Finish(MessagePhase.Done);

    public void Stop() => Finish(MessagePhase.Stopped);

    public void Fail(string error)
    {
        Error = error;
        Phase = MessagePhase.Failed;
    }

    private void Finish(MessagePhase phase)
    {
        var cited = CitationMarkers.CitedNumbers(Text);
        foreach (var citation in Citations)
            citation.IsCited = cited.Contains(citation.Number);

        Phase = phase;
        OnPropertyChanged(nameof(HasStats));
        NotifyCitationsChanged();
    }

    private void NotifyCitationsChanged()
    {
        OnPropertyChanged(nameof(HasCitations));
        OnPropertyChanged(nameof(HasCited));
        OnPropertyChanged(nameof(CitedCitations));
        OnPropertyChanged(nameof(StatsLine));
        OnPropertyChanged(nameof(HasStats));
        CitationsChanged?.Invoke();
    }
}
