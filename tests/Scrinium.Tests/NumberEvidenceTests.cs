using Scrinium.Core.Retrieval;

namespace Scrinium.Tests;

public sealed class NumberEvidenceTests
{
    private static readonly HashSet<string> Nothing = [];

    [Fact]
    public void A_precise_number_outweighs_the_day_of_a_date()
    {
        const string claim = "The target is 99.95 percent from 1 March.";

        Assert.Equal(0.8, NumberEvidence.Overlap(claim, "The availability target is 99.95 percent.", Nothing), 3);
        Assert.Equal(0.2, NumberEvidence.Overlap(claim, "The plans renew on 1 March.", Nothing), 3);
    }

    [Fact]
    public void Numbers_the_question_named_do_not_count()
    {
        var asked = NumberEvidence.Numbers("What is the target in 2026?");

        Assert.Equal(0, NumberEvidence.Overlap("The target in 2026 is 99.95 percent.", "This agreement applies from 2026.", asked));
        Assert.Equal(1, NumberEvidence.Overlap("The target in 2026 is 99.95 percent.", "| Business | 99.95 percent |", asked));
    }

    [Theory]
    [InlineData("1,200", "1200")]
    [InlineData("99,95", "99.95")]
    [InlineData("6 000", "6000")]
    public void Separators_do_not_change_a_number(string written, string other) =>
        Assert.Equal(1, NumberEvidence.Overlap($"It is {written}.", $"It is {other}.", Nothing));

    [Fact]
    public void A_claim_without_numbers_has_no_number_evidence() =>
        Assert.Equal(0, NumberEvidence.Overlap("It is open on Sundays.", "Open 9 to 5.", Nothing));
}
