namespace Scrinium.Core.Chunking;

public sealed record ChunkDraft(int? Page, int Offset, string Text, int TokenCount);
