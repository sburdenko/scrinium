using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scrinium.Desktop.ViewModels;
using Scrinium.Desktop.Views;
using Scrinium.Tests.Support;

namespace Scrinium.UiTests;

public sealed class WorkbenchWindowTests
{
    private const string FreePlan = "The Free plan allows 60 requests per minute.";
    private const string TeamPlan = "The Team plan allows 1200 requests per minute.";

    private static async Task<(DesktopHarness Harness, WindowDriver Driver)> OpenWithTwoPlansAsync(string reply)
    {
        var harness = await DesktopHarness.CreateAsync(new FakeChatClient(reply));
        await harness.AddDocumentAsync("free.txt", FreePlan);
        await harness.AddDocumentAsync("team.txt", TeamPlan);
        return (harness, WindowDriver.Open(harness));
    }

    private static async Task AskThroughTheComposerAsync(WindowDriver driver, DesktopHarness harness, string question)
    {
        var composer = driver.Find<TextBox>(t => t.Name == "Prompt", "composer");
        driver.Type(composer, question);
        driver.Press(Key.Enter);
        await driver.UntilAsync(() => harness.Chat.Exchanges.Count > 0 && !harness.Chat.IsBusy && harness.Chat.FocusedExchange?.Answer.HasStats == true, "the answer");
    }

    [AvaloniaFact]
    public async Task The_window_has_the_archive_the_conversation_and_the_original_side_by_side()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var driver = WindowDriver.Open(harness);

        Assert.Equal(280, driver.Find<Border>(b => b.Name == "ArchiveColumn", "archive column").Bounds.Width);
        Assert.Equal(420, driver.Find<RightPanelView>(_ => true, "original").Bounds.Width);
        Assert.Equal(1320 - 280 - 420, driver.Find<ChatView>(_ => true, "chat").Bounds.Width);
        Assert.True(driver.Shows("Click a citation number to see the passage"));
    }

    [AvaloniaFact]
    public async Task Enter_in_the_composer_asks_and_the_original_opens_on_the_cited_sentence()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("The Free plan allows 60 [1].");
        using var _ = harness;

        await AskThroughTheComposerAsync(driver, harness, "free plan requests per minute");

        Assert.True(driver.Shows("free plan requests per minute"));
        Assert.True(driver.Shows("ORIGINAL · 1 OF 1"));
        Assert.Contains(driver.All<Button>(), b => b.Classes.Contains("cell"));
        Assert.Equal("Session · 1 question", harness.Chat.SessionLine);
        Assert.True(driver.Shows("Session · 1 question"));
    }

    [AvaloniaFact]
    public async Task Shift_Enter_adds_a_line_instead_of_sending()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("unused");
        using var _ = harness;
        var composer = driver.Find<TextBox>(t => t.Name == "Prompt", "composer");
        driver.Type(composer, "first line");

        driver.Press(Key.Enter, Avalonia.Input.RawInputModifiers.Shift);

        Assert.Empty(harness.Chat.Exchanges);
        Assert.Contains('\n', harness.Chat.Input.Replace("\r", ""));
    }

    [AvaloniaFact]
    public async Task Files_dropped_on_the_window_are_added_to_the_archive()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var driver = WindowDriver.Open(harness);
        var first = harness.App.WriteFile("dropped-one.md", "# One\nFirst dropped note.");
        var second = harness.App.WriteFile("dropped-two.txt", "Second dropped note.");

        await driver.DropFilesAsync(driver.Find<ChatView>(_ => true, "chat"), first, second);
        await driver.UntilAsync(() => harness.Sources.Items.Count == 2, "two archive rows");

        Assert.True(driver.Shows("dropped-one.md"));
        Assert.True(driver.Shows("dropped-two.txt"));
    }

    [AvaloniaFact]
    public async Task A_dropped_file_of_an_unsupported_type_is_reported_in_the_archive()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var driver = WindowDriver.Open(harness);
        var image = harness.App.WriteFile("photo.jpg", "not really a photo");

        await driver.DropFilesAsync(driver.Find<ArchiveView>(_ => true, "archive"), image);
        await driver.UntilAsync(() => harness.Sources.LastError.Length > 0, "the import error");

        Assert.Empty(harness.Sources.Items);
        Assert.Contains(driver.All<TextBlock>(), t => t.Text?.Contains("photo.jpg: unsupported format") == true);
    }

    [AvaloniaFact]
    public async Task The_delete_button_of_an_archive_row_shows_only_while_the_row_is_hovered()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("unused");
        using var _ = harness;
        var row = driver.Find<Button>(b => WindowDriver.NameOf(b) == "free.txt", "free.txt row");

        Assert.DoesNotContain(driver.All<Button>(), b => WindowDriver.NameOf(b) == "Delete");

        driver.Hover(row);
        var delete = Assert.Single(driver.All<Button>(), b => WindowDriver.NameOf(b) == "Delete");

        driver.Click(delete);
        await driver.UntilAsync(() => harness.Sources.Items.Count == 1, "the row to disappear");
        driver.HoverAway();

        Assert.False(driver.Shows("free.txt"));
        Assert.True(driver.Shows("team.txt"));
    }

    [AvaloniaFact]
    public async Task Clicking_an_archive_row_opens_the_whole_document_without_a_passage()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("unused");
        using var _ = harness;
        var row = driver.Find<Button>(b => WindowDriver.NameOf(b) == "team.txt", "team.txt row");

        driver.Click(row);
        await driver.UntilAsync(() => driver.Shows("ORIGINAL"), "the original header");

        Assert.Contains("open", row.Classes);
        Assert.DoesNotContain(driver.All<Button>(), b => WindowDriver.NameOf(b) == "Next");
    }

    [AvaloniaFact]
    public async Task Next_and_previous_step_through_the_cited_sources_and_wrap()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("Free allows 60 [1]. Team allows 1200 [2].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "plan requests per minute");
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 1 OF 2"), "the first source");

        driver.Click(driver.Button("Next"));
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 2 OF 2"), "the second source");

        driver.Click(driver.Button("Next"));
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 1 OF 2"), "the wrap to the first source");

        driver.Click(driver.Button("Previous"));
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 2 OF 2"), "the wrap back to the last source");
    }

    [AvaloniaFact]
    public async Task A_cell_in_the_citation_strip_opens_its_source_and_becomes_active()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("Free allows 60 [1]. Team allows 1200 [2].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "plan requests per minute");
        var cells = driver.All<Button>().Where(b => b.Classes.Contains("cell")).ToList();
        Assert.Equal(2, cells.Count);

        driver.Click(cells[1]);
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 2 OF 2"), "the second source");

        Assert.Contains("active", cells[1].Classes);
        Assert.DoesNotContain("active", cells[0].Classes);
    }

    [AvaloniaFact]
    public async Task Settings_replace_the_original_and_closing_them_brings_it_back()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("The Free plan allows 60 [1].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "free plan requests per minute");

        driver.Click(driver.Button("Settings"));
        await driver.UntilAsync(() => driver.Shows("ANSWER MODEL"), "the settings");
        Assert.False(driver.Shows("ORIGINAL · 1 OF 1"));

        driver.Click(driver.Button("Close settings"));
        await driver.UntilAsync(() => driver.Shows("ORIGINAL · 1 OF 1"), "the original");
    }

    [AvaloniaFact]
    public async Task The_settings_button_toggles_the_settings()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var driver = WindowDriver.Open(harness);

        driver.Click(driver.Button("Settings"));
        await driver.UntilAsync(() => driver.Shows("ANSWER MODEL"), "the settings");

        driver.Click(driver.Button("Settings"));
        await driver.UntilAsync(() => driver.Shows("Click a citation number to see the passage"), "the empty original");
    }

    [AvaloniaFact]
    public async Task Clicking_an_earlier_exchange_expands_it_and_collapses_the_latest()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("The Free plan allows 60 [1].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "free plan requests per minute");
        await AskThroughTheComposerAsync(driver, harness, "free plan again");
        await driver.UntilAsync(() => harness.Chat.Exchanges.Count == 2 && !harness.Chat.IsBusy, "two exchanges");

        driver.Click(driver.Button("free plan requests per minute"));
        await driver.UntilAsync(() => harness.Chat.FocusedExchange == harness.Chat.Exchanges[0], "the first exchange in focus");

        Assert.Single(driver.All<Button>(), b => WindowDriver.NameOf(b) == "free plan again");
    }

    [AvaloniaFact]
    public async Task New_question_clears_the_focus_and_the_original_and_focuses_the_composer()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("The Free plan allows 60 [1].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "free plan requests per minute");

        driver.Click(driver.Button("New question"));
        await driver.UntilAsync(() => driver.Find<TextBox>(t => t.Name == "Prompt", "composer").IsFocused, "the composer focus");

        Assert.True(driver.Shows("Click a citation number to see the passage"));
        Assert.True(driver.Shows("Ask a follow-up…") || driver.All<TextBox>().Any(t => t.PlaceholderText == "Ask a follow-up…"));
    }

    [AvaloniaFact]
    public async Task A_saved_session_is_listed_on_an_empty_window_and_reopens_with_a_click()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("The Free plan allows 60 [1].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "free plan requests per minute");
        driver.Window.Close();

        var reopened = await harness.ReopenAsync();
        await reopened.Chat.LoadRecentSessionsAsync();
        var again = WindowDriver.Open(reopened);
        Assert.True(again.Shows("RECENT SESSIONS"));

        again.Click(again.Button("free plan requests per minute"));
        await again.UntilAsync(() => again.Shows("ORIGINAL · 1 OF 1"), "the restored source");

        Assert.False(again.Shows("RECENT SESSIONS"));
        Assert.Single(reopened.Chat.Exchanges);
    }

    [AvaloniaFact]
    public async Task Every_visible_button_has_a_readable_name()
    {
        var (harness, driver) = await OpenWithTwoPlansAsync("Free allows 60 [1]. Team allows 1200 [2].");
        using var _ = harness;
        await AskThroughTheComposerAsync(driver, harness, "plan requests per minute");
        driver.Hover(driver.Find<Button>(b => WindowDriver.NameOf(b) == "free.txt", "free.txt row"));

        var unnamed = driver.All<Button>()
            .Where(b => b.GetVisualAncestors().OfType<ScrollBar>().FirstOrDefault() is null)
            .Select(b => (Button: b, Name: WindowDriver.NameOf(b)))
            .Where(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.StartsWith("Avalonia.", StringComparison.Ordinal))
            .Select(x => $"{x.Button.GetType().Name} '{x.Name}' classes [{string.Join(' ', x.Button.Classes)}]")
            .ToList();

        Assert.Empty(unnamed);
    }
}

public sealed class WorkspacePickerTests
{
    [AvaloniaFact]
    public async Task A_workspace_is_created_from_the_window_and_opens_empty()
    {
        using var harness = await DesktopHarness.CreateAsync();
        await harness.AddDocumentAsync("note.txt", "Engine noise got louder in September.");
        var driver = WindowDriver.Open(harness);
        Assert.True(driver.Shows("note.txt"));

        driver.Click(driver.Button("New workspace"));
        var name = driver.Find<TextBox>(t => t.Name == "WorkspaceName", "workspace name box");
        driver.Type(name, "Kestrel Analytics");
        driver.Press(Key.Enter);
        await driver.UntilAsync(() => harness.Active.Workspace.Name == "Kestrel Analytics", "the new workspace");

        Assert.True(driver.Shows("Nothing here yet"));
        Assert.False(driver.Shows("note.txt"));
        Assert.True(driver.Shows("Kestrel Analytics"));
    }

    [AvaloniaFact]
    public async Task Escape_cancels_naming_a_workspace()
    {
        using var harness = await DesktopHarness.CreateAsync();
        var driver = WindowDriver.Open(harness);

        driver.Click(driver.Button("New workspace"));
        var name = driver.Find<TextBox>(t => t.Name == "WorkspaceName", "workspace name box");
        driver.Type(name, "Draft");
        driver.Press(Key.Escape);

        Assert.False(harness.Main.IsNamingWorkspace);
        Assert.Single(harness.Main.Workspaces);
    }
}
