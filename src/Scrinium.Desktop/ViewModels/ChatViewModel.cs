using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Scrinium.Core.Chat;
using Scrinium.Core.Sessions;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class ChatViewModel : ObservableObject
{
    private const int RecentSessionLimit = 8;

    private readonly AnswerService _answers;
    private readonly PanelHost _panel;
    private readonly ISessionStore _sessions;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _cts;
    private int _historyStart;
    private Guid? _sessionId;

    public ChatViewModel(AnswerService answers, PanelHost panel, ISessionStore sessions, ILogger<ChatViewModel> logger)
    {
        _answers = answers;
        _panel = panel;
        _sessions = sessions;
        _logger = logger;
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

    public ObservableCollection<SessionSummaryViewModel> RecentSessions { get; } = [];

    public bool IsEmpty => Exchanges.Count == 0;

    public bool ShowsRecentSessions => IsEmpty && RecentSessions.Count > 0;

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

        var exchange = Track(new ExchangeViewModel(question, DateTimeOffset.Now));
        Exchanges.Add(exchange);
        FocusedExchange = exchange;
        var sessionId = _sessionId ??= Guid.NewGuid();
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

        await SaveAsync(sessionId, exchange);

        if (ReferenceEquals(FocusedExchange, exchange))
            await ShowFirstSourceAsync(exchange);
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void NewQuestion()
    {
        _historyStart = Exchanges.Count;
        _sessionId = null;
        FocusedExchange = null;
        _panel.ResetOriginal();
        FocusComposerRequested?.Invoke();
    }

    public async Task LoadRecentSessionsAsync()
    {
        IReadOnlyList<SessionSummary> recent;
        try
        {
            recent = await _sessions.ListRecentAsync(RecentSessionLimit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the saved sessions");
            return;
        }

        RecentSessions.Clear();
        foreach (var summary in recent)
            RecentSessions.Add(new SessionSummaryViewModel(summary));

        OnPropertyChanged(nameof(ShowsRecentSessions));
    }

    [RelayCommand]
    private async Task OpenSessionAsync(SessionSummaryViewModel? session)
    {
        if (session is null || IsBusy)
            return;

        IReadOnlyList<SavedExchange> saved;
        try
        {
            saved = await _sessions.LoadAsync(session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open session {SessionId}", session.Id);
            return;
        }

        FocusedExchange = null;
        Exchanges.Clear();
        foreach (var item in saved)
        {
            var exchange = Track(new ExchangeViewModel(item.Question, item.Asked));
            exchange.Answer.Restore(item.Answer, item.Citations, item.Ending == ExchangeEnding.Stopped ? MessagePhase.Stopped : MessagePhase.Done);
            Exchanges.Add(exchange);
        }

        _sessionId = session.Id;
        _historyStart = 0;
        NotifySessionChanged();
        ScrollRequested?.Invoke();

        if (Exchanges.Count > 0)
            await FocusExchangeAsync(Exchanges[^1]);
    }

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

    private ExchangeViewModel Track(ExchangeViewModel exchange)
    {
        exchange.Answer.CitationsChanged += () =>
        {
            if (ReferenceEquals(FocusedExchange, exchange))
                CitationStateChanged?.Invoke();
        };
        return exchange;
    }

    private async Task SaveAsync(Guid sessionId, ExchangeViewModel exchange)
    {
        var answer = exchange.Answer;
        if (!answer.IsFinished)
            return;

        var saved = new SavedExchange(
            exchange.Asked,
            exchange.Question,
            answer.Text,
            answer.IsStopped ? ExchangeEnding.Stopped : ExchangeEnding.Answered,
            answer.Citations.Select(c => c.Source).ToList());
        try
        {
            await _sessions.AppendAsync(sessionId, saved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save the exchange to session {SessionId}", sessionId);
        }
    }

    private void NotifySessionChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowsRecentSessions));
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
