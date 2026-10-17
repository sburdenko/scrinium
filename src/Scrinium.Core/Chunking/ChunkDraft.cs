using Scrinium.Core.Documents;

namespace Scrinium.Core.Chunking;

public sealed record ChunkDraft(int? Page, int Offset, string Text, int TokenCount, string Section, ChunkContent Content)
{
    /// <summary>The text the embedding model sees: the section path, so a chunk deep in a section still says what it is about.</summary>
    public string EmbeddingText => Section.Length == 0 ? Text : $"{Section}\n\n{Text}";
}
