using Scrinium.Core.Chat;

namespace Scrinium.Tests;

public class CitationMarkersTests
{
    [Fact]
    public void Plain_text_has_no_markers() =>
        Assert.IsType<AnswerPart.Plain>(Assert.Single(CitationMarkers.Split("Просто текст.")));

    [Fact]
    public void Marker_is_split_out_of_the_sentence()
    {
        var parts = CitationMarkers.Split("Срок отмены 14 дней [3]. Далее текст.");

        Assert.Equal(3, parts.Count);
        Assert.Equal("Срок отмены 14 дней ", Assert.IsType<AnswerPart.Plain>(parts[0]).Text);
        Assert.Equal([3], Assert.IsType<AnswerPart.Marker>(parts[1]).Numbers);
        Assert.Equal(". Далее текст.", Assert.IsType<AnswerPart.Plain>(parts[2]).Text);
    }

    [Fact]
    public void Several_numbers_in_one_marker_are_supported() =>
        Assert.Equal([1, 4], Assert.IsType<AnswerPart.Marker>(CitationMarkers.Split("[1, 4]")[0]).Numbers);

    [Fact]
    public void Cited_numbers_are_collected_across_markers() =>
        Assert.Equal(new HashSet<int> { 1, 2, 5 }, CitationMarkers.CitedNumbers("a [1] b [2] c [5][1]"));

    [Fact]
    public void Square_brackets_with_words_are_not_markers() =>
        Assert.Empty(CitationMarkers.CitedNumbers("[удалённый документ] и [abc]"));
}
