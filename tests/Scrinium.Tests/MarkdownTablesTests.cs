using Scrinium.Core.Retrieval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public class MarkdownTablesTests
{
    private const string Table = """
        | Feature | Free | Starter | Team |
        |---|---|---|---|
        | Email alerts | not included | not included | included |
        | Audit log | not included | not included | included |
        """;

    [Fact]
    public void Every_row_becomes_a_sentence_that_repeats_the_column_names()
    {
        var flat = MarkdownTables.Flatten(Table);

        Assert.Contains("Email alerts: Free = not included; Starter = not included; Team = included.", flat);
        Assert.Contains("Audit log: Free = not included; Starter = not included; Team = included.", flat);
        Assert.DoesNotContain("|", flat);
    }

    [Fact]
    public void Text_around_a_table_is_kept()
    {
        var flat = MarkdownTables.Flatten("Intro line.\n\n" + Table + "\n\nClosing line.");

        Assert.StartsWith("Intro line.", flat);
        Assert.EndsWith("Closing line.", flat);
    }

    [Fact]
    public void A_table_cut_off_without_its_header_is_left_alone()
    {
        const string cut = "| Email alerts | not included | included |\n| Audit log | not included | included |";

        Assert.Equal(cut, MarkdownTables.Flatten(cut));
    }

    [Fact]
    public void Text_without_tables_is_returned_unchanged()
    {
        const string text = "Plain paragraph with a | pipe in the middle.";

        Assert.Equal(text, MarkdownTables.Flatten(text));
    }

    [Fact]
    public void Empty_cells_are_kept_as_empty_values()
    {
        var flat = MarkdownTables.Flatten("| A | B | C |\n|---|---|---|\n| row | | x |");

        Assert.Contains("row: B = ; C = x.", flat);
    }

    [Fact]
    public async Task The_prompt_gets_the_flattened_table_but_the_citation_still_points_into_the_original()
    {
        using var app = new TestApp(retrieval: new RetrievalOptions(MinScore: 0.0, ScoreMargin: 1.0));
        const string text = "# Features\n\n" + """
            | Feature | Free | Team |
            |---|---|---|
            | Email alerts | not included | included |
            """;
        await app.ImportAndProcessAsync(app.WriteFile("features.md", text));

        var outcome = await app.Retriever.RetrieveAsync("features email alerts team");

        var fragment = Assert.Single(Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments);
        Assert.Contains("Email alerts: Free = not included; Team = included.", fragment.PromptText);
        var extracted = text.ReplaceLineEndings("\n");
        var originalAtLocation = extracted.Substring(fragment.Citation.Location.Offset, fragment.Citation.Location.Length);
        Assert.Equal(fragment.Citation.Preview, originalAtLocation);
    }

    [Fact]
    public async Task A_document_with_windows_line_endings_shows_the_cited_row_in_the_original()
    {
        using var app = new TestApp(retrieval: new RetrievalOptions(MinScore: 0.0, ScoreMargin: 1.0));
        const string text = "# Features\r\n\r\n| Feature | Free | Team |\r\n|---|---|---|\r\n| Email alerts | not included | included |\r\n";
        await app.ImportAndProcessAsync(app.WriteFile("features.md", text));

        var outcome = await app.Retriever.RetrieveAsync("features email alerts team");
        var citation = Assert.Single(Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments).Citation;
        var preview = await app.Preview.LoadAsync(citation.Location);

        Assert.NotNull(preview);
        Assert.Equal(citation.Preview, preview.Highlight);
    }
}
