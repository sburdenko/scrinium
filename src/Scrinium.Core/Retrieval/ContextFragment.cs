namespace Scrinium.Core.Retrieval;

public sealed record ContextFragment(
    Citation Citation,
    string PromptText,
    string ChunkText = "",
    int ChunkOffset = 0,
    string Section = "");
