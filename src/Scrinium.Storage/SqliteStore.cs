using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrinium.Core.Documents;

using Scrinium.Core.Storage;

namespace Scrinium.Storage;

public sealed class SqliteStore : IDocumentStore, IVectorStore
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _connectionString;

    public SqliteStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath))!;
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            ForeignKeys = true
        }.ToString();

        using var connection = Open();
        SqliteSchema.Apply(connection);
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO documents (id, title, path, kind, file_date, added_at, state, embedding_model, chunk_count, error)
            VALUES ($id, $title, $path, $kind, $fileDate, $addedAt, $state, $model, $count, $error)
            """;
        command.Parameters.AddWithValue("$id", document.Id.ToString());
        command.Parameters.AddWithValue("$title", document.Title);
        command.Parameters.AddWithValue("$path", document.StoredPath);
        command.Parameters.AddWithValue("$kind", document.Kind.ToString());
        command.Parameters.AddWithValue("$fileDate", document.FileDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$addedAt", document.AddedAt.ToString("O", CultureInfo.InvariantCulture));
        BindState(command, document.State);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SetStateAsync(Guid documentId, IndexState state, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE documents SET state = $state, embedding_model = $model, chunk_count = $count, error = $error
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", documentId.ToString());
        BindState(command, state);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM documents ORDER BY added_at DESC";
        return await ReadDocumentsAsync(command, ct);
    }

    public async Task<Document?> GetAsync(Guid documentId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM documents WHERE id = $id";
        command.Parameters.AddWithValue("$id", documentId.ToString());
        var documents = await ReadDocumentsAsync(command, ct);
        return documents.FirstOrDefault();
    }

    public async Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<EmbeddedChunk> chunks,
        string embeddingModel,
        CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM chunks WHERE document_id = $id";
            delete.Parameters.AddWithValue("$id", documentId.ToString());
            await delete.ExecuteNonQueryAsync(ct);
        }

        foreach (var embedded in chunks)
        {
            var chunk = embedded.Chunk;
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO chunks (id, document_id, ordinal, page, offset, text, date, token_count, embedding, section, content)
                VALUES ($id, $doc, $ordinal, $page, $offset, $text, $date, $tokens, $embedding, $section, $content)
                """;
            insert.Parameters.AddWithValue("$id", chunk.Id.ToString());
            insert.Parameters.AddWithValue("$doc", chunk.DocumentId.ToString());
            insert.Parameters.AddWithValue("$ordinal", chunk.Ordinal);
            insert.Parameters.AddWithValue("$page", (object?)chunk.Page ?? DBNull.Value);
            insert.Parameters.AddWithValue("$offset", chunk.Offset);
            insert.Parameters.AddWithValue("$text", chunk.Text);
            insert.Parameters.AddWithValue("$date", chunk.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$tokens", chunk.TokenCount);
            insert.Parameters.AddWithValue("$section", chunk.Section);
            insert.Parameters.AddWithValue("$content", chunk.Content.ToString());
            insert.Parameters.AddWithValue("$embedding", VectorMath.ToBlob(VectorMath.Normalize(embedded.Vector)));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE documents SET state = 'Ready', embedding_model = $model, chunk_count = $count, error = NULL
                WHERE id = $id
                """;
            update.Parameters.AddWithValue("$id", documentId.ToString());
            update.Parameters.AddWithValue("$model", embeddingModel);
            update.Parameters.AddWithValue("$count", chunks.Count);
            await update.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Chunk>> GetChunkRangeAsync(
        Guid documentId,
        int fromOrdinal,
        int toOrdinal,
        CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, document_id, ordinal, page, offset, text, date, token_count, section, content FROM chunks
            WHERE document_id = $doc AND ordinal BETWEEN $from AND $to ORDER BY ordinal
            """;
        command.Parameters.AddWithValue("$doc", documentId.ToString());
        command.Parameters.AddWithValue("$from", fromOrdinal);
        command.Parameters.AddWithValue("$to", toOrdinal);

        var result = new List<Chunk>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(ReadChunk(reader));
        return result;
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM chunks WHERE document_id = $id; DELETE FROM documents WHERE id = $id;";
        command.Parameters.AddWithValue("$id", documentId.ToString());
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ChunkHit>> SearchAsync(
        float[] query,
        string embeddingModel,
        int topK,
        CancellationToken ct = default)
    {
        var normalizedQuery = VectorMath.Normalize(query);
        var best = new PriorityQueue<ChunkHit, double>();

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.document_id, c.ordinal, c.page, c.offset, c.text, c.date, c.token_count,
                   c.embedding, d.title, c.section, c.content
            FROM chunks c JOIN documents d ON d.id = c.document_id
            WHERE d.state = 'Ready' AND d.embedding_model = $model
            """;
        command.Parameters.AddWithValue("$model", embeddingModel);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var vector = VectorMath.FromBlob((byte[])reader["embedding"]);
            var score = VectorMath.Dot(normalizedQuery, vector);
            if (double.IsNegativeInfinity(score))
                continue;

            if (best.Count == topK && score <= best.Peek().Score)
                continue;

            best.Enqueue(new ChunkHit(ReadChunk(reader), reader.GetString(9), score), score);
            if (best.Count > topK)
                best.Dequeue();
        }

        var hits = new List<ChunkHit>(best.Count);
        while (best.Count > 0)
            hits.Add(best.Dequeue());
        hits.Reverse();
        return hits;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static void BindState(SqliteCommand command, IndexState state)
    {
        (string Name, string? Model, int Count, string? Error) row = state switch
        {
            IndexState.Pending => ("Pending", null, 0, null),
            IndexState.Indexing => ("Indexing", null, 0, null),
            IndexState.Ready ready => ("Ready", ready.EmbeddingModel, ready.ChunkCount, null),
            IndexState.Failed failed => ("Failed", null, 0, failed.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        var (name, model, count, error) = row;

        command.Parameters.AddWithValue("$state", name);
        command.Parameters.AddWithValue("$model", (object?)model ?? DBNull.Value);
        command.Parameters.AddWithValue("$count", count);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
    }

    private static async Task<IReadOnlyList<Document>> ReadDocumentsAsync(SqliteCommand command, CancellationToken ct)
    {
        var result = new List<Document>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            IndexState state = reader.GetString(reader.GetOrdinal("state")) switch
            {
                "Pending" => new IndexState.Pending(),
                "Indexing" => new IndexState.Indexing(),
                "Ready" => new IndexState.Ready(
                    reader.GetString(reader.GetOrdinal("embedding_model")),
                    reader.GetInt32(reader.GetOrdinal("chunk_count"))),
                _ => new IndexState.Failed(reader.IsDBNull(reader.GetOrdinal("error"))
                    ? "Unknown error"
                    : reader.GetString(reader.GetOrdinal("error")))
            };

            result.Add(new Document(
                Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetString(reader.GetOrdinal("path")),
                Enum.Parse<DocumentKind>(reader.GetString(reader.GetOrdinal("kind"))),
                DateOnly.ParseExact(reader.GetString(reader.GetOrdinal("file_date")), DateFormat, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("added_at")), CultureInfo.InvariantCulture),
                state));
        }

        return result;
    }

    private static Chunk ReadChunk(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        reader.GetInt32(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3),
        reader.GetInt32(4),
        reader.GetString(5),
        DateOnly.ParseExact(reader.GetString(6), DateFormat, CultureInfo.InvariantCulture),
        reader.GetInt32(7),
        reader.GetString(reader.GetOrdinal("section")),
        Enum.Parse<ChunkContent>(reader.GetString(reader.GetOrdinal("content"))));
}
