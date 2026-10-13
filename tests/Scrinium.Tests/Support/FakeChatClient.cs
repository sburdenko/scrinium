using Microsoft.Extensions.AI;

namespace Scrinium.Tests.Support;

public sealed class FakeChatClient(params string[] tokens) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public ChatOptions? LastOptions { get; private set; }

    public string? NonStreamingReply { get; set; }

    public List<IReadOnlyList<ChatMessage>> NonStreamingRequests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        NonStreamingRequests.Add(list);
        var reply = NonStreamingReply ?? list[^1].Text ?? "";
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        LastOptions = options;
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, token);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
