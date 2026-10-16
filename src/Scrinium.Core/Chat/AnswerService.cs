using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;

namespace Scrinium.Core.Chat;

public sealed class AnswerService(
    Retriever retriever,
    QueryRewriter rewriter,
    PassageRefiner refiner,
    IChatClient chat,
    ISettingsProvider settings)
{
    private const float Temperature = 0.2f;

    public async IAsyncEnumerable<AnswerEvent> AskAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var searchQuery = await rewriter.RewriteAsync(question, history, ct);
        var outcome = await retriever.RetrieveAsync(searchQuery, ct);

        if (outcome is not RetrievalOutcome.Found found)
        {
            yield return new AnswerEvent.SourcesFound([]);
            yield return new AnswerEvent.TextDelta(Explain(outcome));
            yield break;
        }

        yield return new AnswerEvent.SourcesFound(found.Fragments.Select(f => f.Citation).ToList());

        var messages = PromptBuilder.Build(question, found.Fragments, history);
        var options = new ChatOptions { ModelId = settings.Current.ChatModel, Temperature = Temperature };

        var raw = new StringBuilder();
        var shown = 0;
        var reply = Reply.Streaming;
        var explained = false;

        await foreach (var update in chat.GetStreamingResponseAsync(messages, options, ct))
        {
            var text = update.Text;
            if (string.IsNullOrEmpty(text))
                continue;

            raw.Append(text);
            switch (reply)
            {
                case Reply.Streaming:
                    var all = raw.ToString();
                    var marker = all.IndexOf(PromptBuilder.DeclineMarker, StringComparison.Ordinal);
                    if (marker >= 0 && all[..marker].Trim().Length == 0)
                    {
                        reply = Reply.DeclinedUpFront;
                        yield return new AnswerEvent.SourcesFound([]);
                        var explanation = all[(marker + PromptBuilder.DeclineMarker.Length)..].TrimStart();
                        if (explanation.Length > 0)
                        {
                            explained = true;
                            yield return new AnswerEvent.TextDelta(explanation);
                        }

                        break;
                    }

                    if (marker >= 0)
                    {
                        reply = Reply.DeclinedLate;
                        break;
                    }

                    var safe = all.Length - HeldBack(all);
                    if (safe > shown)
                    {
                        yield return new AnswerEvent.TextDelta(all[shown..safe]);
                        shown = safe;
                    }

                    break;

                case Reply.DeclinedUpFront:
                    var delta = explained ? text : text.TrimStart();
                    if (delta.Length > 0)
                    {
                        explained = true;
                        yield return new AnswerEvent.TextDelta(delta);
                    }

                    break;
            }
        }

        var fullText = raw.ToString();
        switch (reply)
        {
            case Reply.DeclinedUpFront:
                if (!explained)
                {
                    yield return new AnswerEvent.TextDelta(PromptBuilder.DeclinedMessage);
                    yield break;
                }

                var afterMarker = fullText[(fullText.IndexOf(PromptBuilder.DeclineMarker, StringComparison.Ordinal) + PromptBuilder.DeclineMarker.Length)..].TrimStart();
                if (IsGroundedAnswer(afterMarker, searchQuery, found.Fragments))
                {
                    var initial = found.Fragments.Select(f => f.Citation).ToList();
                    yield return new AnswerEvent.SourcesFound(initial);
                    var recovered = await refiner.RefineAsync(found.Fragments, afterMarker, searchQuery, ct);
                    if (recovered.Zip(found.Fragments, (after, fragment) => after.Location != fragment.Citation.Location).Any(x => x))
                        yield return new AnswerEvent.SourcesRefined(recovered);
                }

                yield break;

            case Reply.DeclinedLate:
                var withoutMarker = CitationMarkers.Strip(fullText.Replace(PromptBuilder.DeclineMarker, " ", StringComparison.Ordinal));
                yield return new AnswerEvent.Declined(withoutMarker.Length > 0 ? withoutMarker : PromptBuilder.DeclinedMessage);
                yield break;
        }

        if (raw.Length > shown)
            yield return new AnswerEvent.TextDelta(raw.ToString(shown, raw.Length - shown));

        if (StatesMissingInDocuments(fullText))
        {
            var cleaned = CitationMarkers.Strip(fullText);
            yield return new AnswerEvent.Declined(cleaned.Length > 0 ? cleaned : PromptBuilder.DeclinedMessage);
            yield break;
        }

        var refined = await refiner.RefineAsync(found.Fragments, fullText, searchQuery, ct);
        var changed = refined.Zip(found.Fragments, (after, fragment) => after.Location != fragment.Citation.Location).Any(x => x);
        if (changed)
            yield return new AnswerEvent.SourcesRefined(refined);
    }

    private static bool IsGroundedAnswer(string text, string question, IReadOnlyList<ContextFragment> fragments)
    {
        if (StatesMissingInDocuments(text))
            return false;

        var cited = CitationMarkers.CitedNumbers(text);
        if (cited.Count == 0)
            return false;

        var clean = CitationMarkers.Strip(text);
        var questionNumbers = NumberEvidence.Numbers(question);
        var questionWords = NumberEvidence.Words(question);

        return fragments
            .Where(f => cited.Contains(f.Citation.Number))
            .Any(f => NumberEvidence.Overlap(clean, f.ChunkText, questionNumbers) > 0
                || NumberEvidence.WordOverlap(clean, f.ChunkText, questionWords) >= 0.4);
    }

    private static readonly string[] MissingPhrases =
    [
        "do not mention",
        "does not mention",
        "not mentioned in the",
        "contain no information",
        "contains no information",
        "do not contain information",
        "no information about",
        "keine informationen darüber",
        "enthalten keine informationen",
        "enthält keine informationen",
        "nicht explizit genannt",
        "nicht explizit aufgeführt",
        "bereitgestellten fragmenten nicht",
        "fragmenten nicht enthalten",
        "keine angaben über",
        "keine angaben zu",
        "nicht in den bereitgestellten fragmenten",
        "не содержат информации",
        "нет информации о"
    ];

    private static bool StatesMissingInDocuments(string text) =>
        MissingPhrases.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    /// <summary>How many trailing characters could still grow into the decline marker and must wait for the next token.</summary>
    private static int HeldBack(string text)
    {
        var marker = PromptBuilder.DeclineMarker;
        for (var length = Math.Min(marker.Length - 1, text.Length); length > 0; length--)
        {
            if (marker.StartsWith(text[^length..], StringComparison.Ordinal))
                return length;
        }

        return 0;
    }

    private enum Reply
    {
        Streaming,
        DeclinedUpFront,
        DeclinedLate
    }

    private static string Explain(RetrievalOutcome outcome) => outcome switch
    {
        RetrievalOutcome.NothingRelevant n => PromptBuilder.NothingRelevantMessage(n.ClosestTitle, n.BestScore),
        _ => PromptBuilder.ArchiveEmptyMessage
    };
}
