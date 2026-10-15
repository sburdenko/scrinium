namespace Scrinium.Server;

public sealed class ServerOptions
{
    public string Home { get; init; } = "scrinium";

    public string? OllamaUrl { get; init; }

    public string? ChatModel { get; init; }

    public string? EmbeddingModel { get; init; }

    public string? DocumentsFolder { get; init; }

    public string? ApiKey { get; init; }

    public string[] AllowedOrigins { get; init; } = [];

    public int ChatRequestsPerMinute { get; init; } = 10;

    public int MaxConcurrentChats { get; init; } = 2;

    public int MaxQuestionLength { get; init; } = 2000;

    public int MaxHistoryItems { get; init; } = 20;

    public WorkspaceOptions[] Workspaces { get; init; } = [];
}

public sealed class WorkspaceOptions
{
    public string Id { get; init; } = "";

    public string? Name { get; init; }

    public string? DocumentsFolder { get; init; }

    public string? ApiKey { get; init; }
}
