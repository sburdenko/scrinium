using Scrinium.Core.Retrieval;

namespace Scrinium.Core.Chat;

public abstract record AnswerEvent
{
    private AnswerEvent() { }

    public sealed record SourcesFound(IReadOnlyList<Citation> Citations) : AnswerEvent;

    public sealed record TextDelta(string Text) : AnswerEvent;
}
