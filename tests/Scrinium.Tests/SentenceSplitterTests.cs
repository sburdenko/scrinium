using Scrinium.Core.Retrieval;

namespace Scrinium.Tests;

public class SentenceSplitterTests
{
    private static IReadOnlyList<string> Texts(string text) =>
        SentenceSplitter.Split(text).Select(span => text.Substring(span.Start, span.Length)).ToList();

    [Fact]
    public void Empty_text_has_no_sentences() =>
        Assert.Empty(SentenceSplitter.Split("  \n  "));

    [Fact]
    public void Text_without_terminal_punctuation_is_one_sentence() =>
        Assert.Equal(["Built the backend integration over REST"], Texts("Built the backend integration over REST"));

    [Fact]
    public void Sentences_split_after_terminal_punctuation()
    {
        var text = "The bearing makes a grinding noise. The belt needs replacing soon! Is the oil level fine?";

        Assert.Equal(
            ["The bearing makes a grinding noise.", "The belt needs replacing soon!", "Is the oil level fine?"],
            Texts(text));
    }

    [Fact]
    public void Bullet_lines_are_separate_sentences()
    {
        var text = "• Built app features and the integration with the backend\n• Implemented UI and navigation for many devices";

        Assert.Equal(
            ["• Built app features and the integration with the backend", "• Implemented UI and navigation for many devices"],
            Texts(text));
    }

    [Fact]
    public void A_line_wrap_inside_a_sentence_does_not_split_it()
    {
        var text = "The tenant may terminate the lease with three\nmonths' written notice to the end of a calendar month.";

        Assert.Single(Texts(text));
    }

    [Fact]
    public void A_line_ending_with_punctuation_ends_the_sentence()
    {
        var text = "The tenant may terminate the lease.\nThe deposit is returned within six weeks.";

        Assert.Equal(["The tenant may terminate the lease.", "The deposit is returned within six weeks."], Texts(text));
    }

    [Fact]
    public void A_heading_line_stays_with_the_line_that_follows_it()
    {
        var text = "Shopping app for a grocery retail chain 2020 – 2022\nClient: grocery retail · mobile · app with backend\n• Built the REST integration";

        var sentences = Texts(text);

        Assert.Equal(2, sentences.Count);
        Assert.StartsWith("Shopping app", sentences[0]);
        Assert.Contains("Client: grocery retail", sentences[0]);
        Assert.StartsWith("• Built", sentences[1]);
    }

    [Fact]
    public void A_long_run_without_punctuation_is_split_at_a_line_break()
    {
        var line = "This line is a fairly long piece of text without any terminal mark";
        var text = string.Join("\n", Enumerable.Repeat(line, 4));

        Assert.True(Texts(text).Count > 1);
    }

    [Fact]
    public void Every_table_row_is_its_own_passage_and_the_separator_row_is_dropped()
    {
        const string text = "| Plan | Requests per minute |\n|---|---|\n| Free | 60 |\n| Team | 1,200 |\n| Business | 6,000 |";

        var rows = Texts(text);

        Assert.Equal(["| Plan | Requests per minute |", "| Free | 60 |", "| Team | 1,200 |", "| Business | 6,000 |"], rows);
    }

    [Fact]
    public void Text_before_and_after_a_table_stays_separate_from_its_rows()
    {
        const string text = "Rate limits apply per key and per minute.\n| Plan | Limit |\n|---|---|\n| Team | 1,200 |\nBursts are tolerated for ten seconds.";

        var passages = Texts(text);

        Assert.Equal("Rate limits apply per key and per minute.", passages[0]);
        Assert.Contains("| Team | 1,200 |", passages);
        Assert.Equal("Bursts are tolerated for ten seconds.", passages[^1]);
    }

    [Fact]
    public void Very_short_fragments_are_merged_with_the_next_sentence()
    {
        var text = "2020 – 2022. Built app features and the integration with the backend";

        var sentence = Assert.Single(Texts(text));
        Assert.Contains("2020 – 2022", sentence);
        Assert.Contains("integration", sentence);
    }

    [Fact]
    public void Spans_are_trimmed_and_point_into_the_original_text()
    {
        var text = "  First sentence is long enough to stand alone.   Second sentence is also long enough.  ";

        var spans = SentenceSplitter.Split(text);

        Assert.Equal(2, spans.Count);
        Assert.All(spans, s => Assert.Equal(text.Substring(s.Start, s.Length).Trim(), text.Substring(s.Start, s.Length)));
        Assert.Equal("Second sentence is also long enough.", text.Substring(spans[1].Start, spans[1].Length));
    }

    [Fact]
    public void A_markdown_heading_is_never_a_passage_and_does_not_stick_to_the_next_one()
    {
        var text = "## Kann ich Unterlagen digital einreichen?\n\nJa. Nutzen Sie das Mandantenportal oder senden Sie die Dateien verschlüsselt.";

        var sentences = Texts(text);

        Assert.Equal(["Ja. Nutzen Sie das Mandantenportal oder senden Sie die Dateien verschlüsselt."], sentences);
    }

    [Fact]
    public void A_blank_line_ends_a_passage_even_without_punctuation()
    {
        var text = "Opening hours on weekdays from nine to six\n\nSaturdays by appointment only for existing clients";

        var sentences = Texts(text);

        Assert.Equal(2, sentences.Count);
        Assert.StartsWith("Saturdays", sentences[1]);
    }

    [Theory]
    [InlineData("Die Erklärung ist bis zum 31. Juli des Folgejahres abzugeben. Danach droht ein Zuschlag.")]
    [InlineData("Die Frist endet am 1. März 2026. Danach droht ein Zuschlag.")]
    [InlineData("The new plan starts on 3. Oct 2026 for every customer. After that the old plan ends.")]
    public void A_day_before_a_month_name_does_not_end_the_sentence(string text)
    {
        var sentences = Texts(text);

        Assert.Equal(2, sentences.Count);
        Assert.StartsWith("Danach", sentences[1].Replace("After", "Danach"));
    }
}
