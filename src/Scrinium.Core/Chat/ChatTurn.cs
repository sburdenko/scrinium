using Microsoft.Extensions.AI;

namespace Scrinium.Core.Chat;

public sealed record ChatTurn(ChatRole Role, string Text)
{
    public static ChatTurn User(string text) => new(ChatRole.User, text);

    public static ChatTurn Assistant(string text) => new(ChatRole.Assistant, text);
}
