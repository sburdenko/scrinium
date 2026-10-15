using Scrinium.Core.Documents;
using Scrinium.Desktop.ViewModels;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task The_model_line_names_the_local_chat_model()
    {
        using var harness = await DesktopHarness.CreateAsync();

        Assert.Equal("Local · qwen3-coder:30b", harness.Main.ModelLine);
    }

    [Fact]
    public async Task The_model_line_follows_a_settings_change()
    {
        using var harness = await DesktopHarness.CreateAsync();

        harness.App.Settings.Update(s => s with { ChatModel = "gemma3:12b" });

        Assert.Equal("Local · gemma3:12b", harness.Main.ModelLine);
    }

    [Fact]
    public async Task Settings_open_in_the_original_column()
    {
        using var harness = await DesktopHarness.CreateAsync();

        harness.Main.ToggleSettingsCommand.Execute(null);

        Assert.IsType<SettingsPanelViewModel>(harness.Panel.Current);
    }

    [Fact]
    public async Task A_new_question_keeps_the_earlier_exchanges_and_clears_the_focus()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("An answer [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");

        harness.Main.NewQuestionCommand.Execute(null);

        Assert.Single(harness.Chat.Exchanges);
        Assert.Null(harness.Chat.FocusedExchange);
    }

    [Fact]
    public async Task The_session_line_counts_questions()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("An answer [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        Assert.Equal("Session · 0 questions", harness.Chat.SessionLine);

        await harness.AskAsync("engine noise louder");
        Assert.Equal("Session · 1 question", harness.Chat.SessionLine);

        await harness.AskAsync("engine noise again");
        Assert.Equal("Session · 2 questions", harness.Chat.SessionLine);
    }
}

public sealed class PanelHostTests
{
    private static CitationViewModel Citation(Guid documentId, int number) =>
        new(new Scrinium.Core.Retrieval.Citation(
            number, documentId, "note.txt", null, new DateOnly(2026, 1, 1), "preview",
            SourceLocation.Start(documentId)));

    [Fact]
    public async Task The_column_starts_on_an_empty_original()
    {
        using var harness = await DesktopHarness.CreateAsync();

        var original = Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);

        Assert.Equal(OriginalState.Empty, original.State);
        Assert.True(original.IsEmpty);
    }

    [Fact]
    public async Task A_citation_shows_the_passage_with_its_position_among_the_citations()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var first = Citation(document.Id, 1);
        var second = Citation(document.Id, 2);

        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(second, [first, second]));

        var original = (PreviewPanelViewModel)harness.Panel.Current;
        Assert.Equal(OriginalState.Passage, original.State);
        Assert.Equal("ORIGINAL · 2 OF 2", original.Kicker);
        Assert.True(original.HasPager);
    }

    [Fact]
    public async Task A_document_opened_from_the_archive_has_no_passage_and_no_pager()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        await harness.Panel.ShowPreviewAsync(PreviewRequest.ForDocument(document.Id, "note.txt"));

        var original = (PreviewPanelViewModel)harness.Panel.Current;
        Assert.Equal(OriginalState.Document, original.State);
        Assert.Equal("ORIGINAL", original.Kicker);
        Assert.False(original.HasPager);
        Assert.Equal(document.Id, original.DocumentId);
    }

    [Fact]
    public async Task A_missing_original_is_reported_as_unavailable()
    {
        using var harness = await DesktopHarness.CreateAsync();

        await harness.Panel.ShowPreviewAsync(PreviewRequest.ForDocument(Guid.NewGuid(), "gone.txt"));

        var original = (PreviewPanelViewModel)harness.Panel.Current;
        Assert.Equal(OriginalState.Unavailable, original.State);
    }

    [Fact]
    public async Task Next_and_previous_wrap_around_the_citations()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var first = Citation(document.Id, 1);
        var second = Citation(document.Id, 2);
        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(second, [first, second]));

        await ((PreviewPanelViewModel)harness.Panel.Current).NextCommand.ExecuteAsync(null);
        Assert.Equal(1, ((PreviewPanelViewModel)harness.Panel.Current).Number);

        await ((PreviewPanelViewModel)harness.Panel.Current).PreviousCommand.ExecuteAsync(null);
        Assert.Equal(2, ((PreviewPanelViewModel)harness.Panel.Current).Number);
    }

    [Fact]
    public async Task Opening_settings_twice_returns_to_the_original()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var before = harness.Panel.Current;

        harness.Panel.ShowSettings();
        Assert.IsType<SettingsPanelViewModel>(harness.Panel.Current);

        harness.Panel.ShowSettings();
        Assert.Same(before, harness.Panel.Current);
    }

    [Fact]
    public async Task Settings_opened_over_an_original_return_to_that_original()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var citation = Citation(document.Id, 1);
        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(citation, [citation]));
        var before = harness.Panel.Current;

        harness.Panel.ShowSettings();
        harness.Panel.Close();

        Assert.Same(before, harness.Panel.Current);
    }

    [Fact]
    public async Task Showing_an_original_while_settings_are_open_replaces_the_settings()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var citation = Citation(document.Id, 1);
        harness.Panel.ShowSettings();

        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(citation, [citation]));

        Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
    }
}

public sealed class ExchangeTests
{
    private static async Task<DesktopHarness> HarnessWithTwoAnswersAsync()
    {
        var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");
        await harness.AskAsync("engine noise louder again");
        return harness;
    }

    [Fact]
    public async Task Each_question_makes_an_exchange_and_the_latest_is_focused()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        Assert.Equal(2, harness.Chat.Exchanges.Count);
        Assert.Same(harness.Chat.Exchanges[^1], harness.Chat.FocusedExchange);
        Assert.True(harness.Chat.Exchanges[^1].IsFocused);
        Assert.False(harness.Chat.Exchanges[0].IsFocused);
    }

    [Fact]
    public async Task An_arriving_answer_opens_its_first_cited_source_in_the_original()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        var original = Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);

        Assert.Equal(OriginalState.Passage, original.State);
        Assert.Equal(1, original.Number);
        Assert.Equal(1, harness.Chat.FocusedExchange!.Answer.ActiveCitationNumber);
    }

    [Fact]
    public async Task Focusing_an_earlier_exchange_expands_it_and_shows_its_source()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var first = harness.Chat.Exchanges[0];

        await harness.Chat.FocusExchangeCommand.ExecuteAsync(first);

        Assert.Same(first, harness.Chat.FocusedExchange);
        Assert.True(first.IsFocused);
        Assert.False(harness.Chat.Exchanges[1].IsFocused);
        Assert.Equal(1, first.Answer.ActiveCitationNumber);
        Assert.Null(harness.Chat.Exchanges[1].Answer.ActiveCitationNumber);
    }

    [Fact]
    public async Task A_compressed_row_carries_the_time_and_a_short_answer_without_markers()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        var row = harness.Chat.Exchanges[0];

        Assert.Matches(@"^\d\d:\d\d$", row.TimeText);
        Assert.Equal("It got louder.", row.ShortAnswer);
    }

    [Fact]
    public async Task The_strip_lists_only_cited_sources()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        var answer = harness.Chat.FocusedExchange!.Answer;

        Assert.All(answer.CitedCitations, c => Assert.True(c.IsCited));
        Assert.Contains(answer.CitedCitations, c => c.Number == 1);
    }

    [Fact]
    public async Task The_stats_line_counts_retrieved_and_cited_fragments()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        Assert.Equal(
            "1 fragment retrieved · 1 cited · answered from your documents only",
            harness.Chat.FocusedExchange!.Answer.StatsLine);
    }

    [Fact]
    public async Task Opening_a_citation_marks_it_active_in_its_exchange_only()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var first = harness.Chat.Exchanges[0];
        var second = harness.Chat.Exchanges[1];

        await harness.Chat.OpenCitationCommand.ExecuteAsync(first.Answer.Citations[0]);

        Assert.Equal(1, first.Answer.ActiveCitationNumber);
        Assert.Null(second.Answer.ActiveCitationNumber);
        Assert.Same(first, harness.Chat.FocusedExchange);
    }

    [Fact]
    public async Task A_new_question_requests_the_composer_focus()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var requested = 0;
        harness.Chat.FocusComposerRequested += () => requested++;

        harness.Chat.NewQuestionCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.Null(harness.Chat.FocusedExchange);
        Assert.DoesNotContain(harness.Chat.Exchanges, e => e.IsFocused);
    }

    [Fact]
    public async Task A_new_question_starts_without_the_earlier_history()
    {
        var chat = new FakeChatClient("It got louder [1].");
        using var harness = await DesktopHarness.CreateAsync(chat);
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder walrus");

        harness.Chat.NewQuestionCommand.Execute(null);
        await harness.AskAsync("engine noise September");

        var lastRequest = chat.Requests[^1];
        Assert.DoesNotContain(lastRequest, m => (m.Text ?? "").Contains("walrus"));
    }

    [Fact]
    public async Task A_follow_up_without_a_new_question_keeps_the_history()
    {
        var chat = new FakeChatClient("It got louder [1].");
        using var harness = await DesktopHarness.CreateAsync(chat);
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder walrus");

        await harness.AskAsync("engine noise September");

        Assert.Contains(chat.Requests[^1], m => (m.Text ?? "").Contains("walrus"));
    }

    [Fact]
    public async Task The_composer_placeholder_changes_after_the_first_question()
    {
        using var harness = await DesktopHarness.CreateAsync();

        Assert.Equal("Ask about your documents…", harness.Chat.ComposerPlaceholder);
    }

    [Fact]
    public async Task The_composer_asks_for_a_follow_up_once_there_is_an_exchange()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        Assert.Equal("Ask a follow-up…", harness.Chat.ComposerPlaceholder);
    }
}

public sealed class ArchiveRowTests
{
    [Fact]
    public async Task A_ready_document_shows_its_type_and_chunk_count()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        var row = Assert.Single(harness.Sources.Items);

        Assert.Equal("TXT · 1 chunk", row.DetailText);
        Assert.Equal("01", row.NumberText);
        Assert.False(row.CanRetry);
    }

    [Fact]
    public async Task A_failed_document_offers_a_retry_and_shows_the_reason_instead_of_the_type()
    {
        using var harness = await DesktopHarness.CreateAsync();
        harness.App.Embedder.FailWith = new HttpRequestException("model server is down");
        await harness.AddDocumentAsync("broken.txt", "This will fail to index.");

        var row = Assert.Single(harness.Sources.Items);

        Assert.True(row.CanRetry);
        Assert.Contains("model server is down", row.DetailText);
        Assert.True(row.IsFailed);
    }

    [Fact]
    public async Task The_header_names_the_count_and_the_embedding_model()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("a.txt", "First note.");
        await harness.AddDocumentAsync("b.txt", "Second note.");

        Assert.Equal("2 · bge-m3", harness.Sources.Summary);
    }

    [Fact]
    public async Task Rows_are_numbered_in_library_order_even_when_filtered()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("alpha.txt", "First note.");
        await harness.AddDocumentAsync("beta.txt", "Second note.");

        harness.Sources.Filter = "beta";

        var row = Assert.Single(harness.Sources.Items);
        Assert.Equal("beta.txt", row.Title);
        Assert.NotEqual("", row.NumberText);
    }

    [Fact]
    public async Task Documents_cited_in_the_focused_answer_carry_their_citation_numbers()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");

        var row = Assert.Single(harness.Sources.Items);

        Assert.Equal("1", row.CitationMarks);
        Assert.True(row.IsOpenInOriginal);
    }

    [Fact]
    public async Task A_new_question_clears_the_marks()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");

        harness.Main.NewQuestionCommand.Execute(null);

        var row = Assert.Single(harness.Sources.Items);
        Assert.Equal("", row.CitationMarks);
    }

    [Fact]
    public async Task Clicking_a_row_opens_that_document_without_a_highlight()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var row = Assert.Single(harness.Sources.Items);

        await harness.Sources.OpenCommand.ExecuteAsync(row);

        var original = (PreviewPanelViewModel)harness.Panel.Current;
        Assert.Equal(OriginalState.Document, original.State);
        Assert.True(harness.Sources.Items.Single().IsOpenInOriginal);
    }

    [Fact]
    public async Task Deleting_the_open_document_empties_the_original()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var row = Assert.Single(harness.Sources.Items);
        await harness.Sources.OpenCommand.ExecuteAsync(row);

        await harness.Sources.DeleteCommand.ExecuteAsync(row);

        var original = (PreviewPanelViewModel)harness.Panel.Current;
        Assert.Equal(OriginalState.Empty, original.State);
    }
}

public sealed class SessionTests
{
    [Fact]
    public async Task A_finished_answer_is_saved_to_the_current_session()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        await harness.AskAsync("engine noise louder");
        await harness.AskAsync("engine noise again");

        var session = Assert.Single(await harness.Sessions.ListRecentAsync(10));
        Assert.Equal("engine noise louder", session.Title);
        Assert.Equal(2, session.QuestionCount);
    }

    [Fact]
    public async Task A_new_question_starts_a_new_session()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");

        harness.Chat.NewQuestionCommand.Execute(null);
        await harness.AskAsync("engine noise September");

        Assert.Equal(2, (await harness.Sessions.ListRecentAsync(10)).Count);
    }

    [Fact]
    public async Task Recent_sessions_appear_on_an_empty_window()
    {
        using var first = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await first.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await first.AskAsync("engine noise louder");

        var reopened = await first.ReopenAsync();
        await reopened.Chat.LoadRecentSessionsAsync();

        var recent = Assert.Single(reopened.Chat.RecentSessions);
        Assert.Equal("engine noise louder", recent.Title);
        Assert.Equal("1 question", recent.QuestionsText);
        Assert.True(reopened.Chat.ShowsRecentSessions);
    }

    [Fact]
    public async Task Opening_a_saved_session_restores_its_exchanges_and_sources()
    {
        using var first = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await first.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await first.AskAsync("engine noise louder");
        await first.AskAsync("engine noise again");

        var reopened = await first.ReopenAsync();
        await reopened.Chat.LoadRecentSessionsAsync();
        await reopened.Chat.OpenSessionCommand.ExecuteAsync(reopened.Chat.RecentSessions[0]);

        Assert.Equal(["engine noise louder", "engine noise again"], reopened.Chat.Exchanges.Select(e => e.Question));
        var focused = reopened.Chat.FocusedExchange!;
        Assert.Same(reopened.Chat.Exchanges[^1], focused);
        Assert.Equal("It got louder [1].", focused.Answer.Text);
        Assert.Contains(focused.Answer.CitedCitations, c => c.Number == 1);
        Assert.False(reopened.Chat.ShowsRecentSessions);
        Assert.IsType<PreviewPanelViewModel>(reopened.Panel.Current);
    }

    [Fact]
    public async Task A_follow_up_in_a_reopened_session_continues_it_with_its_history()
    {
        var chat = new FakeChatClient("It got louder [1].");
        using var first = await DesktopHarness.CreateAsync(chat);
        await first.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await first.AskAsync("engine noise louder walrus");

        var reopened = await first.ReopenAsync();
        await reopened.Chat.LoadRecentSessionsAsync();
        await reopened.Chat.OpenSessionCommand.ExecuteAsync(reopened.Chat.RecentSessions[0]);
        await reopened.AskAsync("engine noise September");

        Assert.Contains(chat.Requests[^1], m => (m.Text ?? "").Contains("walrus"));
        var session = Assert.Single(await reopened.Sessions.ListRecentAsync(10));
        Assert.Equal(2, session.QuestionCount);
    }

    [Fact]
    public async Task A_failed_answer_is_not_saved()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        harness.App.Embedder.FailWith = new HttpRequestException("model server is down");

        await harness.AskAsync("engine noise louder");

        Assert.True(harness.Chat.Exchanges[0].Answer.IsFailed);
        Assert.Empty(await harness.Sessions.ListRecentAsync(10));
    }
}

public sealed class WorkspaceSwitchingTests
{
    [Fact]
    public async Task The_window_opens_the_default_workspace_named_personal()
    {
        using var harness = await DesktopHarness.CreateAsync();

        var workspace = Assert.Single(harness.Main.Workspaces);
        Assert.Equal("Personal", workspace.Name);
        Assert.Equal(workspace, harness.Main.SelectedWorkspace);
        Assert.Equal("default", harness.App.Settings.Current.Workspace);
    }

    [Fact]
    public async Task A_new_workspace_opens_empty_and_is_remembered()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");

        harness.Main.StartNamingWorkspaceCommand.Execute(null);
        harness.Main.NewWorkspaceName = "Kestrel Analytics";
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal("Kestrel Analytics", harness.Active.Workspace.Name);
        Assert.Empty(harness.Sources.Items);
        Assert.Empty(harness.Chat.Exchanges);
        Assert.Empty(harness.Chat.RecentSessions);
        Assert.Equal("kestrel-analytics", harness.App.Settings.Current.Workspace);
        Assert.False(harness.Main.IsNamingWorkspace);
        Assert.Equal(2, harness.Main.Workspaces.Count);
    }

    [Fact]
    public async Task Switching_back_restores_the_documents_and_sessions_of_that_workspace()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");
        harness.Main.NewWorkspaceName = "Work";
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);

        await harness.Main.SwitchToAsync(harness.Main.Workspaces.Single(w => w.Name == "Personal"));

        Assert.Equal("note.txt", Assert.Single(harness.Sources.Items).Title);
        Assert.Equal("engine noise louder", Assert.Single(harness.Chat.RecentSessions).Title);
    }

    [Fact]
    public async Task Selecting_a_workspace_in_the_picker_switches_to_it()
    {
        using var harness = await DesktopHarness.CreateAsync();
        harness.Main.NewWorkspaceName = "Work";
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);
        var personal = harness.Main.Workspaces.Single(w => w.Name == "Personal");

        harness.Main.SelectedWorkspace = personal;
        for (var attempt = 0; attempt < 100 && harness.Active.Workspace != personal; attempt++)
            await Task.Delay(10);

        Assert.Equal(personal, harness.Active.Workspace);
    }

    [Fact]
    public async Task The_last_workspace_is_opened_again_on_the_next_start()
    {
        using var harness = await DesktopHarness.CreateAsync();
        harness.Main.NewWorkspaceName = "Work";
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);

        var reopened = await harness.ReopenAsync();

        Assert.Equal("Work", reopened.Active.Workspace.Name);
    }

    [Fact]
    public async Task A_blank_name_cannot_be_submitted_and_a_too_long_name_is_explained()
    {
        using var harness = await DesktopHarness.CreateAsync();
        harness.Main.StartNamingWorkspaceCommand.Execute(null);

        harness.Main.NewWorkspaceName = "   ";
        Assert.False(harness.Main.CreateWorkspaceCommand.CanExecute(null));

        harness.Main.NewWorkspaceName = new string('x', 61);
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);

        Assert.Contains("at most 60 characters", harness.Main.WorkspaceError);
        Assert.True(harness.Main.IsNamingWorkspace);
        Assert.Single(harness.Main.Workspaces);
    }

    [Fact]
    public async Task An_unreadable_archive_is_reported_instead_of_crashing_the_window()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.Active.Scope.DisposeAsync();
        Directory.Delete(harness.Active.Scope.Paths.Root, recursive: true);

        await harness.Sources.InitializeAsync();

        Assert.StartsWith("Could not read the archive", harness.Sources.LastError);
    }

    [Fact]
    public async Task Two_quick_switches_end_on_the_last_workspace_chosen()
    {
        using var harness = await DesktopHarness.CreateAsync();
        harness.Main.NewWorkspaceName = "Work";
        await harness.Main.CreateWorkspaceCommand.ExecuteAsync(null);
        var personal = harness.Main.Workspaces.Single(w => w.Name == "Personal");
        var work = harness.Main.Workspaces.Single(w => w.Name == "Work");

        await Task.WhenAll(harness.Main.SwitchToAsync(personal), harness.Main.SwitchToAsync(work), harness.Main.SwitchToAsync(personal));

        Assert.Equal(personal, harness.Active.Workspace);
        Assert.Equal(personal, harness.Main.SelectedWorkspace);
    }

    [Fact]
    public async Task A_late_decline_replaces_the_answer_and_drops_its_sources()
    {
        using var harness = await DesktopHarness.CreateAsync(new FakeChatClient("It got louder [1]. ", "Nothing about brakes. ", "[[NONE]]"));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        await harness.AskAsync("engine noise louder");

        var answer = harness.Chat.Exchanges[0].Answer;
        Assert.Equal("It got louder. Nothing about brakes.", answer.Text);
        Assert.Empty(answer.Citations);
        Assert.False(answer.HasCited);
    }
}
