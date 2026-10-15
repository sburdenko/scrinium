namespace Scrinium.Core.Sessions;

public sealed record SessionSummary(Guid Id, DateTimeOffset Started, string Title, int QuestionCount);
