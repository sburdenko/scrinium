using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

/// <summary>How the focused question was searched: what was found, with which score, and what reached the model.</summary>
public sealed class DiagnosticsPanelViewModel : ObservableObject
{
    private ExchangeViewModel? _exchange;

    public DiagnosticsState State => _exchange?.Diagnostics switch
    {
        null => DiagnosticsState.NoQuestion,
        ExchangeDiagnostics.NotRecorded => DiagnosticsState.NotRecorded,
        ExchangeDiagnostics.Searching => DiagnosticsState.Searching,
        _ => DiagnosticsState.Recorded
    };

    public bool HasRecord => State == DiagnosticsState.Recorded;

    public string Message => State switch
    {
        DiagnosticsState.NoQuestion => "Ask a question to see how it was searched.",
        DiagnosticsState.NotRecorded => "Diagnostics are kept only for questions asked since the app started.",
        DiagnosticsState.Searching => "Searching…",
        _ => ""
    };

    public string QueryLine => Record is { } record && _exchange is { } exchange
        ? $"Searched for: {record.Trace.Query}{(record.Trace.Query == exchange.Question ? "" : " (rewritten from the conversation)")}"
        : "";

    public string SettingsLine => Record?.Trace.Options is { } options
        ? string.Create(CultureInfo.InvariantCulture,
            $"min score {options.MinScore:0.00} · margin {options.ScoreMargin:0.00} · top {options.TopK} · {Describe(options.Search)}")
        : "";

    public string OutcomeLine => Record is { } record && _exchange is { } exchange ? Outcome(record.Trace, exchange.Answer) : "";

    public string TimingLine => Record is { } record
        ? string.Create(CultureInfo.InvariantCulture,
            $"search {record.SearchTime.TotalSeconds:0.0} s · answer {(record.AnswerTime is { } answer ? answer.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" : "…")}")
        : "";

    public IReadOnlyList<CandidateRowViewModel> Candidates => Record is { } record
        ? record.Trace.Candidates.Select(candidate => CandidateRowViewModel.From(candidate, record.Trace.Options)).ToList()
        : [];

    private ExchangeDiagnostics.Recorded? Record => _exchange?.Diagnostics as ExchangeDiagnostics.Recorded;

    public void Show(ExchangeViewModel? exchange)
    {
        if (_exchange is not null)
        {
            _exchange.PropertyChanged -= OnExchangeChanged;
            _exchange.Answer.PropertyChanged -= OnAnswerChanged;
        }

        _exchange = exchange;
        if (exchange is not null)
        {
            exchange.PropertyChanged += OnExchangeChanged;
            exchange.Answer.PropertyChanged += OnAnswerChanged;
        }

        Refresh();
    }

    private void OnExchangeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExchangeViewModel.Diagnostics))
            Refresh();
    }

    private void OnAnswerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnswerViewModel.Phase) or nameof(AnswerViewModel.HasCitations))
            OnPropertyChanged(nameof(OutcomeLine));
    }

    private void Refresh() => OnPropertyChanged(string.Empty);

    private static string Outcome(RetrievalTrace trace, AnswerViewModel answer)
    {
        if (trace.Candidates.Count == 0)
            return "The archive is empty · the model was not asked";

        var sent = trace.Candidates.Count(candidate => candidate.Fate == CandidateFate.Sent);
        if (sent == 0)
            return string.Create(CultureInfo.InvariantCulture, $"No fragment reached min score {trace.Options.MinScore:0.00} · the model was not asked");

        var reply = answer.Phase switch
        {
            MessagePhase.Failed => "the answer failed",
            MessagePhase.Stopped => "stopped",
            MessagePhase.Done when !answer.HasCitations => "the model declined",
            MessagePhase.Done => "answered",
            _ => "the model is answering"
        };
        return $"{sent} {(sent == 1 ? "fragment" : "fragments")} sent to the model · {reply}";
    }

    private static string Describe(SearchMode search) => search == SearchMode.Hybrid ? "meaning and words" : "meaning only";
}
