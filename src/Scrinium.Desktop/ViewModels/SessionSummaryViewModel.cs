using System.Globalization;
using Scrinium.Core.Sessions;

namespace Scrinium.Desktop.ViewModels;

public sealed class SessionSummaryViewModel(SessionSummary summary)
{
    public Guid Id { get; } = summary.Id;

    public string Title { get; } = summary.Title;

    public string WhenText { get; } =
        summary.Started.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.InvariantCulture);

    public string QuestionsText { get; } =
        summary.QuestionCount == 1 ? "1 question" : $"{summary.QuestionCount} questions";
}
