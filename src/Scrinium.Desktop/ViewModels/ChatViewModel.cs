using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Chat;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class ChatViewModel : ObservableObject
{
    private readonly AnswerService _answers;
    private readonly PanelHost _panel;
    private CancellationTokenSource? _cts;
    private int _historyStart;

    public ChatViewModel(AnswerService answers, PanelHost panel)
    {
        _answers = answers;
        _panel = panel;
        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PanelHost.Current))
                SyncActiveCitation();
        };
    }

    public ObservableCollection<ExchangeViewModel> Exchanges { get; } = [];

    [ObservableProperty]
    private ExchangeViewModel? _focusedExchange;

    partial void OnFocusedExchangeChanged(ExchangeViewModel? oldValue, ExchangeViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsFocused = false;

        if (newValue is not null)
            newValue.IsFocused = true;

        CitationStateChanged?.Invoke();
    }

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "What did I write about the engine noise in September?",
        "How much did I spend on hosting this year?",
        "What does the contract say about the cancellation period?"
    ];

    public bool IsEmpty => Exchanges.Count == 0;

    public string SessionLine =>
        $"Session · {Exchanges.Count} {(Exchanges.Count == 1 ? "question" : "questions")}";

    public string ComposerPlaceholder => IsEmpty ? "Ask about your documents…" : "Ask a follow-up…";

    public event Action? ScrollRequested;

    public event Action? FocusComposerRequested;

    public event Action? CitationStateChanged;

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

        var history = Exchanges
            .Skip(_historyStart)
            .Where(e => e.Answer.IsFinished && !string.IsNullOrWhiteSpace(e.Answer.Text))
            .SelectMany(e => new[] { ChatTurn.User(e.Question), ChatTurn.Assistant(e.Answer.Text) })
            .ToList();

        var exchange = new ExchangeViewModel(question, DateTimeOffset.Now);
        exchange.Answer.CitationsChanged += () =>
        {
            if (ReferenceEquals(FocusedExchange, exchange))
                CitationStateChanged?.Invoke();
        };

        Exchanges.Add(exchange);
        FocusedExchange = exchange;
        _panel.ResetOriginal();
        NotifySessionChanged();
        ScrollRequested?.Invoke();

        var reply = exchange.Answer;
        _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await foreach (var answerEvent in _answers.AskAsync(question, history, _cts.Token))
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

        if (ReferenceEquals(FocusedExchange, exchange))
            await ShowFirstSourceAsync(exchange);
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void NewQuestion()
    {
        _historyStart = Exchanges.Count;
        FocusedExchange = null;
        _panel.ResetOriginal();
        FocusComposerRequested?.Invoke();
    }

    [RelayCommand]
    private void UseSuggestion(string suggestion) => Input = suggestion;

    [RelayCommand]
    private async Task FocusExchangeAsync(ExchangeViewModel? exchange)
    {
        if (exchange is null)
            return;

        FocusedExchange = exchange;
        await ShowFirstSourceAsync(exchange);
    }

    [RelayCommand]
    private async Task OpenCitationAsync(CitationViewModel? citation)
    {
        if (citation is null)
            return;

        var owner = OwnerOf(citation) ?? FocusedExchange;
        var cited = owner?.Answer.CitedCitations ?? [];
        IReadOnlyList<CitationViewModel> siblings = cited.Contains(citation)
            ? cited
            : owner?.Answer.Citations.ToList() ?? [citation];

        await _panel.ShowPreviewAsync(PreviewRequest.For(citation, siblings));
    }

    private async Task ShowFirstSourceAsync(ExchangeViewModel exchange)
    {
        var first = exchange.Answer.CitedCitations.FirstOrDefault();
        if (first is null)
        {
            _panel.ResetOriginal();
            return;
        }

        await OpenCitationAsync(first);
    }

    private void NotifySessionChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SessionLine));
        OnPropertyChanged(nameof(ComposerPlaceholder));
    }

    private ExchangeViewModel? OwnerOf(CitationViewModel citation) =>
        Exchanges.FirstOrDefault(e => e.Answer.Citations.Contains(citation));

    private void SyncActiveCitation()
    {
        foreach (var exchange in Exchanges)
            exchange.Answer.ActiveCitationNumber = null;

        if (_panel.Current is not PreviewPanelViewModel { Number: { } number } preview)
            return;

        var owner = preview.Citations.Count > 0 ? OwnerOf(preview.Citations[0]) : null;
        if (owner is null)
            return;

        owner.Answer.ActiveCitationNumber = number;
        FocusedExchange = owner;
    }

    private static string Humanize(Exception ex) => ex switch
    {
        HttpRequestException => "Could not reach Ollama. Make sure it is running (see the banner at the top).",
        _ => ex.Message
    };
}
