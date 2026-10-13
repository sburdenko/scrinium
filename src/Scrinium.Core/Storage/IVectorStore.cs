using Scrinium.Core.Documents;

namespace Scrinium.Core.Storage;

public interface IVectorStore
{
    Task<IReadOnlyList<ChunkHit>> SearchAsync(
        float[] query,
        string embeddingModel,
        int topK,
        CancellationToken ct = default);
}
