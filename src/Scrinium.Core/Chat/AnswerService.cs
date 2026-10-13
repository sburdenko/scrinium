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

        var pending = new StringBuilder();
        var answer = new StringBuilder();
        var decided = false;
        var declined = false;
        var declinedText = false;

        await foreach (var update in chat.GetStreamingResponseAsync(messages, options, ct))
        {
            var text = update.Text;
            if (string.IsNullOrEmpty(text))
                continue;

            if (decided)
            {
                var delta = declined && !declinedText ? text.TrimStart() : text;
                if (delta.Length == 0)
                    continue;

                declinedText |= declined;
                answer.Append(delta);
                yield return new AnswerEvent.TextDelta(delta);
                continue;
            }

            pending.Append(text);
            var head = pending.ToString().TrimStart();

            if (head.StartsWith(PromptBuilder.DeclineMarker, StringComparison.Ordinal))
            {
                decided = declined = true;
                yield return new AnswerEvent.SourcesFound([]);

                var explanation = head[PromptBuilder.DeclineMarker.Length..].TrimStart();
                if (explanation.Length > 0)
                {
                    declinedText = true;
                    yield return new AnswerEvent.TextDelta(explanation);
                }

                continue;
            }

            if (PromptBuilder.DeclineMarker.StartsWith(head, StringComparison.Ordinal))
                continue;

            decided = true;
            answer.Append(pending);
            yield return new AnswerEvent.TextDelta(pending.ToString());
        }

        if (!decided && pending.Length > 0)
        {
            answer.Append(pending);
            yield return new AnswerEvent.TextDelta(pending.ToString());
        }
        else if (declined && !declinedText)
        {
            yield return new AnswerEvent.TextDelta(PromptBuilder.DeclinedMessage);
        }

        if (declined)
            yield break;

        var refined = await refiner.RefineAsync(found.Fragments, answer.ToString(), ct);
        var changed = refined.Zip(found.Fragments, (after, fragment) => after.Location != fragment.Citation.Location).Any(x => x);
        if (changed)
            yield return new AnswerEvent.SourcesRefined(refined);
    }

    private static string Explain(RetrievalOutcome outcome) => outcome switch
    {
        RetrievalOutcome.NothingRelevant n => PromptBuilder.NothingRelevantMessage(n.ClosestTitle, n.BestScore),
        _ => PromptBuilder.ArchiveEmptyMessage
    };
}
