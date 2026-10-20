using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public abstract record ExchangeDiagnostics
{
    private ExchangeDiagnostics() { }

    /// <summary>A question restored from a saved session: its search was not kept.</summary>
    public sealed record NotRecorded : ExchangeDiagnostics;

    public sealed record Searching : ExchangeDiagnostics;

    public sealed record Recorded(RetrievalTrace Trace, TimeSpan SearchTime, TimeSpan? AnswerTime) : ExchangeDiagnostics;
}
