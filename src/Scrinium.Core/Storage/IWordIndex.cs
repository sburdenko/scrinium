using Scrinium.Core.Documents;

namespace Scrinium.Core.Storage;

public interface IWordIndex
{
    /// <summary>
    /// Chunks of ready documents embedded with <paramref name="embeddingModel"/> that contain any of the terms,
    /// best match first; each term is matched as plain text, never as query syntax.
    /// </summary>
    Task<IReadOnlyList<WordMatch>> FindAsync(
        IReadOnlyList<string> terms,
        string embeddingModel,
        int topK,
        CancellationToken ct = default);
}
