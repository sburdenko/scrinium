using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Sessions;
using Scrinium.Storage;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteSessionStore _sessions;

    public SessionStoreTests() => _sessions = new SqliteSessionStore(Path.Combine(_temp.Path, "test.db"));

    public void Dispose() => _temp.Dispose();

    private static SavedExchange Exchange(string question, DateTimeOffset asked, params Citation[] citations) =>
        new(asked, question, $"Answer to {question} [1].", ExchangeEnding.Answered, citations);

    private static Citation Citation(int number)
    {
        var documentId = Guid.NewGuid();
        return new Citation(number, documentId, "api-reference.md", 3, new DateOnly(2026, 10, 1), "Free | 60",
            new SourceLocation(documentId, 3, 120, 9));
    }

    [Fact]
    public async Task A_session_is_named_after_its_first_question()
    {
        var id = Guid.NewGuid();
        var asked = new DateTimeOffset(2026, 10, 15, 13, 45, 0, TimeSpan.Zero);

        await _sessions.AppendAsync(id, Exchange("What is the rate limit?", asked));
        await _sessions.AppendAsync(id, Exchange("And on the Free plan?", asked.AddMinutes(1)));

        var summary = Assert.Single(await _sessions.ListRecentAsync(10));
        Assert.Equal(id, summary.Id);
        Assert.Equal("What is the rate limit?", summary.Title);
        Assert.Equal(asked, summary.Started);
        Assert.Equal(2, summary.QuestionCount);
    }

    [Fact]
    public async Task Exchanges_come_back_in_order_with_their_citations()
    {
        var id = Guid.NewGuid();
        var asked = DateTimeOffset.UtcNow;
        var citation = Citation(1);

        await _sessions.AppendAsync(id, Exchange("first", asked, citation));
        await _sessions.AppendAsync(id, Exchange("second", asked.AddSeconds(5)) with { Ending = ExchangeEnding.Stopped });

        var exchanges = await _sessions.LoadAsync(id);

        Assert.Equal(["first", "second"], exchanges.Select(e => e.Question));
        Assert.Equal(citation, Assert.Single(exchanges[0].Citations));
        Assert.Equal(ExchangeEnding.Stopped, exchanges[1].Ending);
    }

    [Fact]
    public async Task Recent_sessions_are_newest_first_and_limited()
    {
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        for (var day = 0; day < 4; day++)
            await _sessions.AppendAsync(Guid.NewGuid(), Exchange($"day {day}", start.AddDays(day)));

        var recent = await _sessions.ListRecentAsync(2);

        Assert.Equal(["day 3", "day 2"], recent.Select(s => s.Title));
    }

    [Fact]
    public async Task An_unknown_session_loads_as_empty()
    {
        Assert.Empty(await _sessions.LoadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Sessions_survive_reopening_the_database()
    {
        var path = Path.Combine(_temp.Path, "test.db");
        var id = Guid.NewGuid();
        await _sessions.AppendAsync(id, Exchange("kept", DateTimeOffset.UtcNow));

        var reopened = new SqliteSessionStore(path);

        Assert.Equal("kept", Assert.Single(await reopened.LoadAsync(id)).Question);
    }

    [Fact]
    public async Task The_document_store_and_the_session_store_share_one_database()
    {
        var path = Path.Combine(_temp.Path, "test.db");
        var documents = new SqliteStore(path);
        await _sessions.AppendAsync(Guid.NewGuid(), Exchange("shared", DateTimeOffset.UtcNow));

        Assert.Empty(await documents.ListAsync());
        Assert.Single(await _sessions.ListRecentAsync(5));
    }

    [Fact]
    public async Task A_database_from_before_sessions_gains_the_session_tables_and_keeps_its_documents()
    {
        var path = Path.Combine(_temp.Path, "old.db");
        var documents = new SqliteStore(path);
        await documents.AddAsync(new Document(
            Guid.NewGuid(), "kept.md", "originals/kept.md", DocumentKind.Markdown,
            new DateOnly(2026, 9, 14), DateTimeOffset.UtcNow, new IndexState.Pending()));
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var downgrade = connection.CreateCommand();
            downgrade.CommandText = "DROP TABLE session_exchanges; DROP TABLE sessions; ALTER TABLE chunks DROP COLUMN section; PRAGMA user_version = 1;";
            downgrade.ExecuteNonQuery();
        }

        var sessions = new SqliteSessionStore(path);
        await sessions.AppendAsync(Guid.NewGuid(), Exchange("after upgrade", DateTimeOffset.UtcNow));

        Assert.Single(await sessions.ListRecentAsync(5));
        Assert.Equal("kept.md", Assert.Single(await new SqliteStore(path).ListAsync()).Title);
    }
}
