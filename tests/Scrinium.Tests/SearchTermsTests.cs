using Scrinium.Core.Retrieval;

namespace Scrinium.Tests;

public class SearchTermsTests
{
    [Theory]
    [InlineData("What does error E507 mean?", "E507")]
    [InlineData("How fast is the first response to a P1 ticket?", "P1")]
    [InlineData("Which header carries X-Brightmoor-Key?", "X-Brightmoor-Key")]
    [InlineData("What does --dry-run do?", "--dry-run")]
    [InlineData("Which variable is BRIGHTMOOR_ENV?", "BRIGHTMOOR_ENV")]
    [InlineData("Where is .brightmoor/conflicts kept?", ".brightmoor/conflicts")]
    [InlineData("Is HMAC used for webhooks?", "HMAC")]
    [InlineData("What changed in the 2025 bulletin?", "2025")]
    [InlineData("Что значит ошибка E507?", "E507")]
    public void An_identifier_in_the_question_is_a_search_term(string question, string term) =>
        Assert.Equal([term], SearchTerms.Identifiers(question));

    [Theory]
    [InlineData("How do I pause the agent for three hours?")]
    [InlineData("Как установить консольную утилиту Brightmoor?")]
    [InlineData("Wie viel darf ein Hotelzimmer in Deutschland kosten?")]
    [InlineData("Is it open on day 3 of the week?")]
    public void A_question_in_plain_words_has_no_search_terms(string question) =>
        Assert.Empty(SearchTerms.Identifiers(question));

    [Fact]
    public void Quoted_and_backticked_text_is_one_term()
    {
        Assert.Equal(["make e2e", "the losing copy"], SearchTerms.Identifiers("How long does `make e2e` take, and where is \"the losing copy\"?"));
    }

    [Fact]
    public void A_term_is_named_once()
    {
        Assert.Equal(["E507"], SearchTerms.Identifiers("E507 or e507, what is E507?"));
    }
}
