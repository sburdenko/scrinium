using Scrinium.Core.Documents;
using Scrinium.Desktop.ViewModels;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void The_window_starts_on_the_ask_screen()
    {
        using var harness = new DesktopHarness();

        Assert.Equal(AppSection.Ask, harness.Main.Section);
        Assert.True(harness.Main.IsAsk);
        Assert.False(harness.Main.IsArchive);
    }

    [Fact]
    public void The_archive_and_ask_screens_switch_with_their_commands()
    {
        using var harness = new DesktopHarness();

        harness.Main.ShowArchiveCommand.Execute(null);
        Assert.True(harness.Main.IsArchive);

        harness.Main.ShowAskCommand.Execute(null);
        Assert.True(harness.Main.IsAsk);
    }

    [Fact]
    public async Task A_new_chat_clears_the_conversation_and_returns_to_ask()
    {
        using var harness = new DesktopHarness(new FakeChatClient("An answer [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");
        harness.Main.ShowArchiveCommand.Execute(null);

        harness.Main.NewChatCommand.Execute(null);

        Assert.True(harness.Main.IsAsk);
        Assert.Empty(harness.Chat.Messages);
    }

    [Fact]
    public void Settings_open_in_the_right_column_of_the_ask_screen()
    {
        using var harness = new DesktopHarness();
        harness.Main.ShowArchiveCommand.Execute(null);

        harness.Main.ToggleSettingsCommand.Execute(null);

        Assert.True(harness.Main.IsAsk);
        Assert.IsType<SettingsPanelViewModel>(harness.Panel.Current);
    }

    [Fact]
    public async Task The_archive_tab_badge_counts_failed_documents()
    {
        using var harness = new DesktopHarness();
        harness.App.Embedder.FailWith = new HttpRequestException("model server is down");
        await harness.AddDocumentAsync("broken.txt", "This will fail to index.");

        Assert.Equal("1 failed", harness.Main.FailureBadge);
    }

    [Fact]
    public async Task The_archive_tab_has_no_badge_when_nothing_failed()
    {
        using var harness = new DesktopHarness();
        await harness.AddDocumentAsync("fine.txt", "This indexes well.");

        Assert.Null(harness.Main.FailureBadge);
    }
}

public sealed class PanelHostTests
{
    private static CitationViewModel Citation(DesktopHarness harness, Guid documentId, int number) =>
        new(new Scrinium.Core.Retrieval.Citation(
            number, documentId, "note.txt", null, new DateOnly(2026, 1, 1), "preview",
            SourceLocation.Start(documentId)));

    [Fact]
    public void The_panel_starts_collapsed()
    {
        using var harness = new DesktopHarness();

        Assert.True(harness.Panel.IsCollapsed);
        Assert.False(harness.Panel.IsOpen);
        Assert.Null(harness.Panel.Current);
    }

    [Fact]
    public async Task Showing_a_preview_opens_the_panel_and_closing_it_collapses_it()
    {
        using var harness = new DesktopHarness();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var citation = Citation(harness, document.Id, 1);

        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(citation, [citation]));
        Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
        Assert.True(harness.Panel.IsOpen);

        harness.Panel.Close();
        Assert.True(harness.Panel.IsCollapsed);
        Assert.True(harness.Panel.CanReopen);
    }

    [Fact]
    public async Task A_collapsed_panel_reopens_with_the_last_shown_original()
    {
        using var harness = new DesktopHarness();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var citation = Citation(harness, document.Id, 2);
        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(citation, [citation]));
        harness.Panel.Close();

        harness.Panel.Expand();

        var preview = Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
        Assert.Equal(2, preview.Number);
    }

    [Fact]
    public void Settings_opened_from_a_collapsed_panel_return_to_collapsed()
    {
        using var harness = new DesktopHarness();

        harness.Panel.ShowSettings();
        Assert.IsType<SettingsPanelViewModel>(harness.Panel.Current);

        harness.Panel.Close();
        Assert.True(harness.Panel.IsCollapsed);
    }

    [Fact]
    public async Task Settings_opened_over_an_original_return_to_that_original()
    {
        using var harness = new DesktopHarness();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var citation = Citation(harness, document.Id, 1);
        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(citation, [citation]));
        var before = harness.Panel.Current;

        harness.Panel.ShowSettings();
        harness.Panel.Close();

        Assert.Same(before, harness.Panel.Current);
    }

    [Fact]
    public void Opening_settings_twice_closes_them()
    {
        using var harness = new DesktopHarness();

        harness.Panel.ShowSettings();
        harness.Panel.ShowSettings();

        Assert.True(harness.Panel.IsCollapsed);
    }

    [Fact]
    public async Task The_citation_switcher_opens_another_citation_of_the_same_answer()
    {
        using var harness = new DesktopHarness();
        var document = await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var first = Citation(harness, document.Id, 1);
        var second = Citation(harness, document.Id, 2);
        await harness.Panel.ShowPreviewAsync(PreviewRequest.For(first, [first, second]));
        var preview = (PreviewPanelViewModel)harness.Panel.Current!;

        await preview.SwitchToCommand.ExecuteAsync(second);

        var switched = Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
        Assert.Equal(2, switched.Number);
        Assert.Equal([1, 2], switched.Citations.Select(c => c.Number));
    }
}

public sealed class ChatViewModelFocusTests
{
    private static async Task<DesktopHarness> HarnessWithTwoAnswersAsync()
    {
        var harness = new DesktopHarness(new FakeChatClient("It got louder [1]."));
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        await harness.AskAsync("engine noise louder");
        await harness.AskAsync("engine noise louder again");
        return harness;
    }

    [Fact]
    public async Task The_latest_answer_is_focused_and_feeds_the_notes()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        var assistants = harness.Chat.Messages.Where(m => m.IsAssistant).ToList();

        Assert.Same(assistants[^1], harness.Chat.FocusedMessage);
        Assert.NotEmpty(harness.Chat.FocusedMessage!.Citations);
    }

    [Fact]
    public async Task Focusing_an_earlier_answer_changes_the_notes()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var first = harness.Chat.Messages.First(m => m.IsAssistant);

        harness.Chat.FocusMessageCommand.Execute(first);

        Assert.Same(first, harness.Chat.FocusedMessage);
    }

    [Fact]
    public async Task Opening_a_citation_marks_it_active_in_its_answer_only()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var first = harness.Chat.Messages.First(m => m.IsAssistant);
        var second = harness.Chat.Messages.Last(m => m.IsAssistant);

        await harness.Chat.OpenCitationCommand.ExecuteAsync(first.Citations[0]);

        Assert.Equal(first.Citations[0].Number, first.ActiveCitationNumber);
        Assert.Null(second.ActiveCitationNumber);
        Assert.Same(first, harness.Chat.FocusedMessage);
        Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
    }

    [Fact]
    public async Task Collapsing_the_panel_clears_the_active_citation()
    {
        using var harness = await HarnessWithTwoAnswersAsync();
        var answer = harness.Chat.FocusedMessage!;
        await harness.Chat.OpenCitationCommand.ExecuteAsync(answer.Citations[0]);

        harness.Panel.Close();

        Assert.Null(answer.ActiveCitationNumber);
    }

    [Fact]
    public async Task Expanding_without_a_previous_original_opens_citation_one_of_the_focused_answer()
    {
        using var harness = await HarnessWithTwoAnswersAsync();

        await harness.Chat.ExpandOriginalCommand.ExecuteAsync(null);

        var preview = Assert.IsType<PreviewPanelViewModel>(harness.Panel.Current);
        Assert.Equal(harness.Chat.FocusedMessage!.Citations[0].Number, preview.Number);
    }
}

public sealed class ArchiveRowTests
{
    [Fact]
    public async Task A_ready_document_is_labelled_ready_with_its_chunk_count()
    {
        using var harness = new DesktopHarness();
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");

        var row = Assert.Single(harness.Sources.Items);

        Assert.Equal("Ready", row.StateLabel);
        Assert.Equal("TXT", row.KindLabel);
        Assert.False(row.CanRetry);
    }

    [Fact]
    public async Task A_failed_document_offers_a_retry_and_shows_the_reason()
    {
        using var harness = new DesktopHarness();
        harness.App.Embedder.FailWith = new HttpRequestException("model server is down");
        await harness.AddDocumentAsync("broken.txt", "This will fail to index.");

        var row = Assert.Single(harness.Sources.Items);

        Assert.Equal("Failed", row.StateLabel);
        Assert.True(row.CanRetry);
        Assert.Contains("model server is down", row.StatusText);
    }

    [Fact]
    public async Task The_archive_summary_names_the_count_and_the_embedding_model()
    {
        using var harness = new DesktopHarness();
        await harness.AddDocumentAsync("a.txt", "First note.");
        await harness.AddDocumentAsync("b.txt", "Second note.");

        Assert.Equal("2 documents · stays on this computer · bge-m3", harness.Sources.Summary);
    }

    [Fact]
    public async Task A_single_document_is_not_pluralised()
    {
        using var harness = new DesktopHarness();
        await harness.AddDocumentAsync("a.txt", "First note.");

        Assert.StartsWith("1 document ·", harness.Sources.Summary);
    }
}
