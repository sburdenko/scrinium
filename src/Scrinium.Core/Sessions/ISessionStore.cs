namespace Scrinium.Core.Sessions;

/// <summary>Keeps finished question-and-answer exchanges, grouped into sessions, across restarts.</summary>
public interface ISessionStore
{
    /// <summary>Adds an exchange to the end of a session, creating the session on its first exchange.</summary>
    Task AppendAsync(Guid sessionId, SavedExchange exchange, CancellationToken ct = default);

    /// <summary>The latest sessions, newest first.</summary>
    Task<IReadOnlyList<SessionSummary>> ListRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>The exchanges of a session in the order they were asked; empty for an unknown session.</summary>
    Task<IReadOnlyList<SavedExchange>> LoadAsync(Guid sessionId, CancellationToken ct = default);
}
