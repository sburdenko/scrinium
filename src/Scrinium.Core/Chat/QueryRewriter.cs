using Microsoft.Extensions.AI;
using Scrinium.Core.Settings;

namespace Scrinium.Core.Chat;

public sealed class QueryRewriter(IChatClient chat, ISettingsProvider settings)
{
    private const int HistoryTurnsForRewrite = 4;
    private const int LongestQuery = 300;

    private const string Instruction = """
        Rewrite the user's last message as one standalone search query that can be understood without the conversation.
        Use the earlier messages only to fill in what the last message leaves out, such as the subject it refers to.
        Do not answer the question and do not guess any numbers.
        Keep the language of the last message. Reply with the query only, with no explanation and no quotes.
        """;

    public async Task<string> RewriteAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        if (history.Count == 0)
            return question;

        var messages = new List<ChatMessage> { new(ChatRole.System, Instruction) };
        messages.AddRange(history.TakeLast(HistoryTurnsForRewrite).Select(turn => new ChatMessage(turn.Role, turn.Text)));
        messages.Add(new ChatMessage(ChatRole.User, question));

        var options = new ChatOptions { ModelId = settings.Current.ChatModel, Temperature = 0f };
        var response = await chat.GetResponseAsync(messages, options, ct);

        var query = response.Text.Trim().Trim('"', '\'', '“', '”').Trim();
        if (query.Length is <= 0 or > LongestQuery)
            return question;

        var mentionedNumbers = Retrieval.NumberEvidence.Numbers(question);
        foreach (var turn in history)
            mentionedNumbers.UnionWith(Retrieval.NumberEvidence.Numbers(turn.Text));

        return Retrieval.NumberEvidence.StripUnmentionedNumbers(query, mentionedNumbers);
    }
}
