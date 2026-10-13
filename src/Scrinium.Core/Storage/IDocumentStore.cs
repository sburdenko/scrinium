using Scrinium.Core.Documents;

namespace Scrinium.Core.Storage;

public interface IDocumentStore
{
    Task AddAsync(Document document, CancellationToken ct = default);

    Task SetStateAsync(Guid documentId, IndexState state, CancellationToken ct = default);

    Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default);

    Task<Document?> GetAsync(Guid documentId, CancellationToken ct = default);

    Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<EmbeddedChunk> chunks,
        string embeddingModel,
        CancellationToken ct = default);

    Task<IReadOnlyList<Chunk>> GetChunkRangeAsync(
        Guid documentId,
        int fromOrdinal,
        int toOrdinal,
        CancellationToken ct = default);

    Task DeleteAsync(Guid documentId, CancellationToken ct = default);
}
