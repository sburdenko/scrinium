using Scrinium.Core.Retrieval;

namespace Scrinium.Core.Chat;

public abstract record AnswerEvent
{
    private AnswerEvent() { }

    /// <summary>What the search looked at, sent before anything else so a window can show why an answer used what it used.</summary>
    public sealed record Retrieved(RetrievalTrace Trace) : AnswerEvent;

    public sealed record SourcesFound(IReadOnlyList<Citation> Citations) : AnswerEvent;

    public sealed record TextDelta(string Text) : AnswerEvent;

    public sealed record SourcesRefined(IReadOnlyList<Citation> Citations) : AnswerEvent;

    /// <summary>The model declined after it had started answering: replace the streamed text with this one and drop the sources.</summary>
    public sealed record Declined(string Text) : AnswerEvent;
}
