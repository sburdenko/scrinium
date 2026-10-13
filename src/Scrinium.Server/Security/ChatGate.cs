namespace Scrinium.Server.Security;

public sealed class ChatGate(int maxConcurrent)
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _slots = new(Math.Max(1, maxConcurrent));

    public async Task<bool> TryEnterAsync(CancellationToken ct) => await _slots.WaitAsync(MaxWait, ct);

    public void Leave() => _slots.Release();
}
