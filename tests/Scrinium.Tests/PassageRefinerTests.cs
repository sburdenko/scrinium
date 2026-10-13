using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class PassageRefinerTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private const string Chunk =
        "| Plan | Requests per minute |\n|---|---|\n| Free | 60 |\n| Starter | 300 |\n| Team | 1,200 |\n| Business | 6,000 |";

    private static ContextFragment Fragment(string chunk, int offset = 100, int number = 1)
    {
        var id = Guid.NewGuid();
        var citation = new Citation(number, id, "api.md", null, new DateOnly(2026, 1, 1), "initial",
            new SourceLocation(id, null, offset, 5));
        return new ContextFragment(citation, chunk, chunk, offset);
    }

    private PassageRefiner Refiner => new(_app.Embedder, _app.Settings);

    [Fact]
    public async Task The_citation_moves_to_the_row_that_holds_the_claimed_number()
    {
        var fragment = Fragment(Chunk);

        var refined = await Refiner.RefineAsync([fragment], "The Team plan allows 1,200 requests per minute [1].");

        var citation = Assert.Single(refined);
        var located = Chunk.Substring(citation.Location.Offset - 100, citation.Location.Length);
        Assert.Equal("| Team | 1,200 |", located);
        Assert.Equal("| Team | 1,200 |", citation.Preview);
    }

    [Fact]
    public async Task The_number_decides_between_rows_that_are_otherwise_alike()
    {
        var fragment = Fragment(Chunk);

        var refined = await Refiner.RefineAsync([fragment], "Business allows 6,000 requests per minute [1].");

        Assert.Equal("| Business | 6,000 |", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_fragment_that_is_not_cited_keeps_its_citation()
    {
        var fragment = Fragment(Chunk);

        var refined = await Refiner.RefineAsync([fragment], "The Team plan allows 1,200 requests per minute.");

        Assert.Equal("initial", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_chunk_with_one_passage_is_left_alone()
    {
        var fragment = Fragment("Only one sentence here.");

        var refined = await Refiner.RefineAsync([fragment], "Only one sentence here [1].");

        Assert.Equal("initial", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task Each_cited_fragment_is_refined_by_its_own_claim()
    {
        var first = Fragment(Chunk, offset: 0, number: 1);
        var second = Fragment("The invoice is due in 14 days. The service credit is 10 percent of the fee.", offset: 500, number: 2);

        var refined = await Refiner.RefineAsync(
            [first, second], "Team allows 1,200 requests per minute [1]. The credit is 10 percent [2].");

        Assert.Equal("| Team | 1,200 |", refined[0].Preview);
        Assert.Equal("The service credit is 10 percent of the fee.", refined[1].Preview);
    }
}
