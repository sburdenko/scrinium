namespace Scrinium.Server.Endpoints;

internal static class ChatRequestValidator
{
    private const int MaxHistoryTextLength = 8000;

    public static string? Validate(ChatRequest request, ServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return "The question is empty.";

        if (request.Question.Length > options.MaxQuestionLength)
            return $"The question is longer than {options.MaxQuestionLength} characters.";

        var history = request.History ?? [];
        if (history.Count > options.MaxHistoryItems)
            return $"The history has more than {options.MaxHistoryItems} messages.";

        foreach (var item in history)
        {
            if (item.Role is not ("user" or "assistant"))
                return "A history message has a role other than user or assistant.";
            if (string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > MaxHistoryTextLength)
                return "A history message is empty or too long.";
        }

        return null;
    }
}
