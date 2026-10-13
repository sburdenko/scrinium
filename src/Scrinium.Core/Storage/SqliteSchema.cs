using Microsoft.Data.Sqlite;

namespace Scrinium.Core.Storage;

internal static class SqliteSchema
{
    private const int Version = 1;

    public static void Apply(SqliteConnection connection)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(read.ExecuteScalar());
        if (current >= Version)
            return;

        using var create = connection.CreateCommand();
        create.CommandText = $"""
            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                path TEXT NOT NULL,
                kind TEXT NOT NULL,
                file_date TEXT NOT NULL,
                added_at TEXT NOT NULL,
                state TEXT NOT NULL,
                embedding_model TEXT NULL,
                chunk_count INTEGER NOT NULL DEFAULT 0,
                error TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS chunks (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL,
                page INTEGER NULL,
                offset INTEGER NOT NULL,
                text TEXT NOT NULL,
                date TEXT NOT NULL,
                token_count INTEGER NOT NULL,
                embedding BLOB NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_chunks_document ON chunks(document_id, ordinal);
            PRAGMA user_version = {Version};
            """;
        create.ExecuteNonQuery();
    }
}
