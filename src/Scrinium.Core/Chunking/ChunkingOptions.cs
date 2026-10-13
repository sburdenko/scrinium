namespace Scrinium.Core.Chunking;

public sealed record ChunkingOptions(int MaxTokens = 400, int OverlapTokens = 50);
