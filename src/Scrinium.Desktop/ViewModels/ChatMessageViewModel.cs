using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Chat;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public enum MessagePhase
{
    Searching,
    Composing,
    Writing,
    Done,
    Stopped,
    Failed
}

public sealed partial class ChatMessageViewModel : ObservableObject
{
    private ChatMessageViewModel(bool isUser, string text, MessagePhase phase)
    {
        IsUser = isUser;
        _text = text;
        _phase = phase;
    }

    public static ChatMessageViewModel FromUser(string text) => new(true, text, MessagePhase.Done);

    public static ChatMessageViewModel NewAssistant() => new(false, "", MessagePhase.Searching);

    public bool IsUser { get; }

    public bool IsAssistant => !IsUser;

    public ObservableCollection<CitationViewModel> Citations { get; } = [];

    public bool HasCitations => Citations.Count > 0;

    public bool IsPending => Phase is MessagePhase.Searching or MessagePhase.Composing;

    public string StatusLine => Phase switch
    {
        MessagePhase.Searching => "Searching your documents…",
        MessagePhase.Composing when Citations.Count == 0 => "No matching fragments found…",
        MessagePhase.Composing => $"Found {Citations.Count} fragments. Writing the answer…",
        _ => ""
    };

    public bool IsWriting => Phase == MessagePhase.Writing;

    public bool IsFailed => Phase == MessagePhase.Failed;

    public bool IsStopped => Phase == MessagePhase.Stopped;

    public bool IsTextVisible => IsUser || Phase is MessagePhase.Writing or MessagePhase.Done or MessagePhase.Stopped;

    public bool IsFinished => Phase is MessagePhase.Done or MessagePhase.Stopped;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(StatusLine), nameof(IsWriting), nameof(IsFailed), nameof(IsStopped), nameof(IsTextVisible), nameof(IsFinished))]
    private MessagePhase _phase;

    [ObservableProperty]
    private string _error = "";

    public void ApplySources(IReadOnlyList<Citation> citations)
    {
        Citations.Clear();
        foreach (var citation in citations)
            Citations.Add(new CitationViewModel(citation));

        OnPropertyChanged(nameof(HasCitations));
        Phase = MessagePhase.Composing;
        OnPropertyChanged(nameof(StatusLine));
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
    }
}
