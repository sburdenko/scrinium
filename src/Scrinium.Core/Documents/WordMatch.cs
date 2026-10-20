namespace Scrinium.Core.Documents;

/// <summary>A chunk found by its words, with its stored embedding so its meaning can be scored against the question.</summary>
public sealed record WordMatch(Chunk Chunk, string DocumentTitle, float[] Embedding);
