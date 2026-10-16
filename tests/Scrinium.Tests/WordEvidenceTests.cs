using Scrinium.Core.Retrieval;

namespace Scrinium.Tests;

public sealed class WordEvidenceTests
{
    [Fact]
    public void Short_words_are_not_content_words()
    {
        Assert.Equal(["export", "historical", "data", "from"], WordEvidence.ContentWords("To export the historical data from it"));
    }

    [Fact]
    public void Likeness_is_the_share_of_content_words_two_texts_have_in_common()
    {
        const string claim = "ACME Retail should first export the historical data from the old tools as CSV files.";

        var step = WordEvidence.Likeness(claim, "First, export the historical data from the old tools as CSV files.");
        var title = WordEvidence.Likeness(claim, "Note from the customer success team: migration project for ACME Retail");

        Assert.Equal(2.0 * 7 / (10 + 7), step, 3);
        Assert.Equal(2.0 * 3 / (10 + 9), title, 3);
    }

    [Fact]
    public void Word_forms_with_the_same_stem_count_as_the_same_word()
    {
        Assert.Equal(1, WordEvidence.Likeness("Annual plans are refunded", "annual plan refunds"), 3);
    }

    [Fact]
    public void Texts_without_content_words_are_not_alike() =>
        Assert.Equal(0, WordEvidence.Likeness("It is", "So it is"));

    [Fact]
    public void Coverage_is_the_share_of_the_answers_content_words_found_in_the_source()
    {
        Assert.Equal(1, WordEvidence.Coverage("Individual students are not covered", "Individual students are not covered by it."), 3);
        Assert.Equal(1.0 / 3, WordEvidence.Coverage("Students receive vouchers", "Students are not covered."), 3);
    }

    [Fact]
    public void Remaining_keeps_the_words_the_other_text_lacks() =>
        Assert.Equal("remaining months", WordEvidence.Remaining("After that the remaining months", "After that, it ends."));
}
