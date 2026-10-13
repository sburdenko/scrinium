using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Chat;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class ChatViewModel(AnswerService answers, PanelHost panel) : ObservableObject
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "What did I write about the engine noise in September?",
        "How much did I spend on hosting this year?",
        "What does the contract say about the cancellation period?"
    ];

    public bool IsEmpty => Messages.Count == 0;

    public event Action? ScrollRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _input = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    private bool _isBusy;

    public bool CanStop => IsBusy;

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Input);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var question = Input.Trim();
        Input = "";

        var history = Messages
            .Where(m => m.IsFinished && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.IsUser ? ChatTurn.User(m.Text) : ChatTurn.Assistant(m.Text))
            .ToList();

        Messages.Add(ChatMessageViewModel.FromUser(question));
        var reply = ChatMessageViewModel.NewAssistant();
        Messages.Add(reply);
        OnPropertyChanged(nameof(IsEmpty));
        ScrollRequested?.Invoke();

        _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await foreach (var answerEvent in answers.AskAsync(question, history, _cts.Token))
            {
                switch (answerEvent)
                {
                    case AnswerEvent.SourcesFound sources:
                        reply.ApplySources(sources.Citations);
                        break;
                    case AnswerEvent.TextDelta delta:
                        reply.Append(delta.Text);
                        break;
                    case AnswerEvent.SourcesRefined refined:
                        reply.RefineSources(refined.Citations);
                        break;
                }

                ScrollRequested?.Invoke();
            }

            reply.Complete();
        }
        catch (OperationCanceledException)
        {
            reply.Stop();
        }
        catch (Exception ex)
        {
            reply.Fail(Humanize(ex));
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            ScrollRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void NewChat()
    {
        _cts?.Cancel();
        Messages.Clear();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void UseSuggestion(string suggestion) => Input = suggestion;

    [RelayCommand]
    private async Task OpenCitationAsync(CitationViewModel? citation)
    {
        if (citation is not null)
            await panel.ShowPreviewAsync(citation.Location, citation.DocumentTitle);
    }

    private static string Humanize(Exception ex) => ex switch
    {
        HttpRequestException => "Could not reach Ollama. Make sure it is running (see the banner at the top).",
        _ => ex.Message
    };
}
