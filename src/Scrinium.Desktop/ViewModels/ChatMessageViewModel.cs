using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Chat;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class ChatMessageViewModel : ObservableObject
{
    private ChatMessageViewModel(bool isUser, string text, MessagePhase phase, bool ruleAbove = false)
    {
        IsUser = isUser;
        _text = text;
        _phase = phase;
        ShowRuleAbove = ruleAbove;
        Asked = DateTimeOffset.Now;
    }

    public static ChatMessageViewModel FromUser(string text, bool ruleAbove = false) =>
        new(true, text, MessagePhase.Done, ruleAbove);

    public bool ShowRuleAbove { get; }

    public static ChatMessageViewModel NewAssistant() => new(false, "", MessagePhase.Searching);

    public bool IsUser { get; }

    public DateTimeOffset Asked { get; }

    public string MetaLine => $"You asked · {Asked:HH:mm}";

    public string StatsLine
    {
        get
        {
            var cited = Citations.Count(c => c.IsCited);
            var fragments = Citations.Count == 1 ? "1 fragment retrieved" : $"{Citations.Count} fragments retrieved";
            return $"{fragments} · {cited} cited · Answered from your documents only";
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

    public bool IsAssistant => !IsUser;

    public ObservableCollection<CitationViewModel> Citations { get; } = [];

    public bool HasCitations => Citations.Count > 0;

    public bool IsPending => Phase is MessagePhase.Searching or MessagePhase.Composing;

    public string StatusLine => Phase switch
    {
        MessagePhase.Searching => "Searching your documents…",
        MessagePhase.Composing when Citations.Count == 0 => "No matching fragments found…",
        MessagePhase.Composing => $"Found {Citations.Count} {(Citations.Count == 1 ? "fragment" : "fragments")}. Writing the answer…",
        _ => ""
    };

    public bool IsWriting => Phase == MessagePhase.Writing;

    public bool IsFailed => Phase == MessagePhase.Failed;

    public bool IsStopped => Phase == MessagePhase.Stopped;

    public bool IsTextVisible => IsUser || Phase is MessagePhase.Writing or MessagePhase.Done or MessagePhase.Stopped;

    public bool IsFinished => Phase is MessagePhase.Done or MessagePhase.Stopped;

    public string SourcesHeader
    {
        get
        {
            if (!IsFinished)
                return $"Retrieved fragments ({Citations.Count})";

            var cited = Citations.Count(c => c.IsCited);
            return cited switch
            {
                0 => $"Retrieved fragments, none cited ({Citations.Count})",
                _ when cited == Citations.Count => $"Sources ({cited})",
                _ => $"Sources ({cited} cited, {Citations.Count - cited} more retrieved)"
            };
        }
    }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(StatusLine), nameof(SourcesHeader), nameof(IsWriting), nameof(IsFailed), nameof(IsStopped), nameof(IsTextVisible), nameof(IsFinished))]
    private MessagePhase _phase;

    [ObservableProperty]
    private string _error = "";

    public void ApplySources(IReadOnlyList<Citation> citations)
    {
        Citations.Clear();
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation));

        OnPropertyChanged(nameof(HasCitations));
        OnPropertyChanged(nameof(SourcesHeader));
        OnActiveCitationNumberChanged(ActiveCitationNumber);
        Phase = MessagePhase.Composing;
        OnPropertyChanged(nameof(StatusLine));
    }

    public void RefineSources(IReadOnlyList<Citation> citations)
    {
        Citations.Clear();
        var cited = CitationMarkers.CitedNumbers(Text);
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation) { IsCited = cited.Contains(citation.Number) });

        OnActiveCitationNumberChanged(ActiveCitationNumber);
        OnPropertyChanged(nameof(SourcesHeader));
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
        OnPropertyChanged(nameof(SourcesHeader));
        OnPropertyChanged(nameof(StatsLine));
        OnPropertyChanged(nameof(HasStats));
    }
}
