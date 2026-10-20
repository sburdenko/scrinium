using Scrinium.Desktop.ViewModels;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class DiagnosticsPanelTests
{
    private const string Note = "Engine noise got louder in September.";

    private static async Task<DesktopHarness> AnsweredAsync(string reply = "It got louder [1].", params string[] questions)
    {
        var harness = await DesktopHarness.CreateAsync(new FakeChatClient(reply));
        await harness.AddDocumentAsync("note.txt", Note);
        foreach (var question in questions.DefaultIfEmpty("engine noise louder"))
            await harness.AskAsync(question);
        return harness;
    }

    [Fact]
    public async Task The_panel_starts_closed_and_the_toggle_opens_and_closes_it()
    {
        using var harness = await DesktopHarness.CreateAsync();

        Assert.False(harness.Main.IsDiagnosticsOpen);
        harness.Main.ToggleDiagnosticsCommand.Execute(null);
        Assert.True(harness.Main.IsDiagnosticsOpen);
        harness.Main.ToggleDiagnosticsCommand.Execute(null);
        Assert.False(harness.Main.IsDiagnosticsOpen);
    }

    [Fact]
    public async Task Before_any_question_the_panel_asks_for_one()
    {
        using var harness = await DesktopHarness.CreateAsync();

        var panel = harness.Chat.Diagnostics;

        Assert.Equal(DiagnosticsState.NoQuestion, panel.State);
        Assert.Equal("Ask a question to see how it was searched.", panel.Message);
        Assert.Empty(panel.Candidates);
    }

    [Fact]
    public async Task An_answered_question_lists_its_candidates_with_score_and_fate()
    {
        using var harness = await AnsweredAsync();

        var panel = harness.Chat.Diagnostics;

        Assert.Equal(DiagnosticsState.Recorded, panel.State);
        var row = Assert.Single(panel.Candidates);
        Assert.Equal(("note.txt", "meaning", "sent"), (row.Document, row.Source, row.Fate));
        Assert.Matches(@"^0\.\d{3}$", row.Score);
        Assert.Equal("Searched for: engine noise louder", panel.QueryLine);
        Assert.Equal("min score 0.35 · margin 0.10 · top 6 · meaning and words", panel.SettingsLine);
        Assert.Equal("1 fragment sent to the model · answered", panel.OutcomeLine);
        Assert.Matches(@"^search \d+\.\d s · answer \d+\.\d s$", panel.TimingLine);
    }

    [Fact]
    public async Task A_question_below_the_minimum_score_says_the_model_was_not_asked()
    {
        using var harness = await AnsweredAsync(questions: "рыжий кот любит молоко");

        var panel = harness.Chat.Diagnostics;

        Assert.Equal("No fragment reached min score 0.35 · the model was not asked", panel.OutcomeLine);
        Assert.Equal("below min score", Assert.Single(panel.Candidates).Fate);
    }

    [Fact]
    public async Task A_reply_that_declines_is_named_as_a_decline()
    {
        using var harness = await AnsweredAsync("[[NONE]] The notes do not say.");

        Assert.Equal("1 fragment sent to the model · the model declined", harness.Chat.Diagnostics.OutcomeLine);
    }

    [Fact]
    public async Task The_panel_follows_the_focused_exchange()
    {
        using var harness = await AnsweredAsync(questions: ["engine noise louder", "engine noise September"]);

        await harness.Chat.FocusExchangeCommand.ExecuteAsync(harness.Chat.Exchanges[0]);

        Assert.Equal("Searched for: engine noise louder", harness.Chat.Diagnostics.QueryLine);
    }

    [Fact]
    public async Task A_new_question_returns_the_panel_to_its_empty_state()
    {
        using var harness = await AnsweredAsync();

        harness.Chat.NewQuestionCommand.Execute(null);

        Assert.Equal(DiagnosticsState.NoQuestion, harness.Chat.Diagnostics.State);
    }

    [Fact]
    public async Task A_reopened_session_says_its_diagnostics_were_not_recorded()
    {
        using var first = await AnsweredAsync();

        var reopened = await first.ReopenAsync();
        await reopened.Chat.LoadRecentSessionsAsync();
        await reopened.Chat.OpenSessionCommand.ExecuteAsync(reopened.Chat.RecentSessions[0]);

        Assert.Equal(DiagnosticsState.NotRecorded, reopened.Chat.Diagnostics.State);
        Assert.Equal("Diagnostics are kept only for questions asked since the app started.", reopened.Chat.Diagnostics.Message);
    }
}
