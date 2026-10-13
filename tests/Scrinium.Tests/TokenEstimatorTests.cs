using Scrinium.Core.Chunking;

namespace Scrinium.Tests;

public class TokenEstimatorTests
{
    [Fact]
    public void Empty_text_is_zero_tokens() =>
        Assert.Equal(0, TokenEstimator.Estimate(""));

    [Fact]
    public void Ascii_counts_four_chars_per_token() =>
        Assert.Equal(25, TokenEstimator.Estimate(new string('a', 100)));

    [Fact]
    public void Cyrillic_costs_more_than_ascii() =>
        Assert.True(TokenEstimator.Estimate(new string('я', 100)) > TokenEstimator.Estimate(new string('a', 100)));
}
