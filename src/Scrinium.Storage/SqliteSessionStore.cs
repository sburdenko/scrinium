using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Sessions;

namespace Scrinium.Storage;

public sealed class SqliteSessionStore : ISessionStore
{
    private readonly string _connectionString;

    public SqliteSessionStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            ForeignKeys = true
        }.ToString();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        SqliteSchema.Apply(connection);
    }

    public async Task AppendAsync(Guid sessionId, SavedExchange exchange, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using (var session = connection.CreateCommand())
        {
            session.Transaction = transaction;
            session.CommandText = "INSERT OR IGNORE INTO sessions (id, started, title) VALUES ($id, $started, $title)";
            session.Parameters.AddWithValue("$id", sessionId.ToString());
            session.Parameters.AddWithValue("$started", Format(exchange.Asked));
            session.Parameters.AddWithValue("$title", exchange.Question);
            await session.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO session_exchanges (session_id, position, asked, question, answer, ending, citations)
                VALUES ($id,
                        (SELECT COALESCE(MAX(position), -1) + 1 FROM session_exchanges WHERE session_id = $id),
                        $asked, $question, $answer, $ending, $citations)
                """;
            insert.Parameters.AddWithValue("$id", sessionId.ToString());
            insert.Parameters.AddWithValue("$asked", Format(exchange.Asked));
            insert.Parameters.AddWithValue("$question", exchange.Question);
            insert.Parameters.AddWithValue("$answer", exchange.Answer);
            insert.Parameters.AddWithValue("$ending", exchange.Ending.ToString());
            insert.Parameters.AddWithValue("$citations", JsonSerializer.Serialize(exchange.Citations));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<SessionSummary>> ListRecentAsync(int limit, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.started, s.title, COUNT(e.position)
            FROM sessions s LEFT JOIN session_exchanges e ON e.session_id = s.id
            GROUP BY s.id
            ORDER BY MAX(e.asked) DESC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var sessions = new List<SessionSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            sessions.Add(new SessionSummary(
                Guid.Parse(reader.GetString(0)),
                Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return sessions;
    }

    public async Task<IReadOnlyList<SavedExchange>> LoadAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT asked, question, answer, ending, citations
            FROM session_exchanges WHERE session_id = $id ORDER BY position
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString());

        var exchanges = new List<SavedExchange>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            exchanges.Add(new SavedExchange(
                Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                Enum.Parse<ExchangeEnding>(reader.GetString(3)),
                JsonSerializer.Deserialize<List<Citation>>(reader.GetString(4)) ?? []));
        }

        return exchanges;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
