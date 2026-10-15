using Scrinium.Core.Retrieval;

namespace Scrinium.Core.Sessions;

public sealed record SavedExchange(
    DateTimeOffset Asked,
    string Question,
    string Answer,
    ExchangeEnding Ending,
    IReadOnlyList<Citation> Citations);
