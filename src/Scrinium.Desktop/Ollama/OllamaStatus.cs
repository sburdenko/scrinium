namespace Scrinium.Desktop.Ollama;

public abstract record OllamaStatus
{
    private OllamaStatus() { }

    public sealed record Checking : OllamaStatus;

    public sealed record Offline : OllamaStatus;

    public sealed record MissingModels(IReadOnlyList<string> Models) : OllamaStatus;

    public sealed record Pulling(string Model, string Progress) : OllamaStatus;

    public sealed record Ready : OllamaStatus;
}
