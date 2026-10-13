using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;

namespace Scrinium.Core.Chat;

public sealed class AnswerService(
    Retriever retriever,
    IChatClient chat,
    ISettingsProvider settings)
{
    private const float Temperature = 0.2f;

    public async IAsyncEnumerable<AnswerEvent> AskAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var outcome = await retriever.RetrieveAsync(question, ct);

        if (outcome is not RetrievalOutcome.Found found)
        {
            yield return new AnswerEvent.SourcesFound([]);
            yield return new AnswerEvent.TextDelta(Explain(outcome));
            yield break;
        }

        yield return new AnswerEvent.SourcesFound(found.Fragments.Select(f => f.Citation).ToList());

        var messages = PromptBuilder.Build(question, found.Fragments, history);
        var options = new ChatOptions { ModelId = settings.Current.ChatModel, Temperature = Temperature };

        await foreach (var update in chat.GetStreamingResponseAsync(messages, options, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return new AnswerEvent.TextDelta(update.Text);
        }
    }

    private static string Explain(RetrievalOutcome outcome) => outcome switch
    {
        RetrievalOutcome.NothingRelevant n => PromptBuilder.NothingRelevantMessage(n.ClosestTitle, n.BestScore),
        _ => PromptBuilder.ArchiveEmptyMessage
    };
}
