namespace Scrinium.Core.Documents;

public abstract record IndexState
{
    private IndexState() { }

    public sealed record Pending : IndexState;

    public sealed record Indexing : IndexState;

    public sealed record Ready(string EmbeddingModel, int ChunkCount) : IndexState;

    public sealed record Failed(string Error) : IndexState;
}
