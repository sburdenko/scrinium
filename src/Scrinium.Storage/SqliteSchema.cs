using Microsoft.Data.Sqlite;

namespace Scrinium.Storage;

internal static class SqliteSchema
{
    private const int Version = 2;

    public static void Apply(SqliteConnection connection)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(read.ExecuteScalar());
        if (current >= Version)
            return;

        using var transaction = connection.BeginTransaction();
        if (current < 1)
            Execute(connection, transaction, DocumentsAndChunks);
        if (current < 2)
            Execute(connection, transaction, Sessions);

        Execute(connection, transaction, $"PRAGMA user_version = {Version};");
        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private const string DocumentsAndChunks = """
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
        """;

    private const string Sessions = """
        CREATE TABLE IF NOT EXISTS sessions (
            id TEXT PRIMARY KEY,
            started TEXT NOT NULL,
            title TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS session_exchanges (
            session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
            position INTEGER NOT NULL,
            asked TEXT NOT NULL,
            question TEXT NOT NULL,
            answer TEXT NOT NULL,
            ending TEXT NOT NULL,
            citations TEXT NOT NULL,
            PRIMARY KEY (session_id, position)
        );
        CREATE INDEX IF NOT EXISTS ix_sessions_started ON sessions(started);
        """;
}
