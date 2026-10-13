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
}
