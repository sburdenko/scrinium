namespace Scrinium.Core.Documents;

public sealed record Chunk(
    Guid Id,
    Guid DocumentId,
    int Ordinal,
    int? Page,
    int Offset,
    string Text,
    DateOnly Date,
    int TokenCount);

public sealed record EmbeddedChunk(Chunk Chunk, float[] Vector);

public sealed record ChunkHit(Chunk Chunk, string DocumentTitle, double Score);
