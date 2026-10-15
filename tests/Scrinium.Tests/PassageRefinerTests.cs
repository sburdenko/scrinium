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

        var refined = await Refiner.RefineAsync([fragment], "The Team plan allows 1,200 requests per minute [1].", "Team requests per minute");

        var citation = Assert.Single(refined);
        var located = Chunk.Substring(citation.Location.Offset - 100, citation.Location.Length);
        Assert.Equal("| Team | 1,200 |", located);
        Assert.Equal("| Team | 1,200 |", citation.Preview);
    }

    [Fact]
    public async Task The_number_decides_between_rows_that_are_otherwise_alike()
    {
        var fragment = Fragment(Chunk);

        var refined = await Refiner.RefineAsync([fragment], "Business allows 6,000 requests per minute [1].", "");

        Assert.Equal("| Business | 6,000 |", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_fragment_that_is_not_cited_keeps_its_citation()
    {
        var fragment = Fragment(Chunk);

        var refined = await Refiner.RefineAsync([fragment], "The Team plan allows 1,200 requests per minute.", "");

        Assert.Equal("initial", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_chunk_with_one_passage_is_left_alone()
    {
        var fragment = Fragment("Only one sentence here.");

        var refined = await Refiner.RefineAsync([fragment], "Only one sentence here [1].", "");

        Assert.Equal("initial", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task Each_cited_fragment_is_refined_by_its_own_claim()
    {
        var first = Fragment(Chunk, offset: 0, number: 1);
        var second = Fragment("The invoice is due in 14 days. The service credit is 10 percent of the fee.", offset: 500, number: 2);

        var refined = await Refiner.RefineAsync(
            [first, second], "Team allows 1,200 requests per minute [1]. The credit is 10 percent [2].", "");

        Assert.Equal("| Team | 1,200 |", refined[0].Preview);
        Assert.Equal("The service credit is 10 percent of the fee.", refined[1].Preview);
    }

    private const string Availability =
        "## Availability\n\nPaid plans are covered by a monthly availability target of 99.5 percent. " +
        "If the monthly availability falls below the target, the customer receives a credit of 5 percent of that month's fee.";

    [Fact]
    public async Task A_decimal_number_in_the_answer_stays_one_claim()
    {
        var fragment = Fragment(Availability);

        var refined = await Refiner.RefineAsync([fragment],
            "In 2025, paid plans were covered by a monthly availability target of 99.5 percent [1].", "What availability did paid plans guarantee in 2025?");

        Assert.Equal("Paid plans are covered by a monthly availability target of 99.5 percent.", Assert.Single(refined).Preview);
    }

    private const string Export =
        "Wenn Sie Ihr Konto kündigen, können Sie Ihre Daten noch 30 Tage nach Vertragsende exportieren. " +
        "Danach werden alle Daten endgültig gelöscht. Eine Wiederherstellung ist nicht möglich.";

    [Fact]
    public async Task Every_sentence_since_the_previous_marker_is_part_of_the_claim()
    {
        var fragment = Fragment(Export);

        var refined = await Refiner.RefineAsync([fragment],
            "Nach der Kündigung können Sie Ihre Daten noch 30 Tage lang exportieren. Danach werden die Daten gelöscht [1].", "Wie lange kann ich meine Daten nach der Kündigung exportieren?");

        Assert.Equal(
            "Wenn Sie Ihr Konto kündigen, können Sie Ihre Daten noch 30 Tage nach Vertragsende exportieren.",
            Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task Text_before_an_earlier_marker_is_not_part_of_the_next_claim()
    {
        var first = Fragment(Availability, number: 1);
        var second = Fragment(Export, number: 2);

        var refined = await Refiner.RefineAsync([first, second],
            "The target is 99.5 percent [1]. Data can be exported for 30 days [2].", "");

        Assert.Equal("Paid plans are covered by a monthly availability target of 99.5 percent.", refined[0].Preview);
        Assert.StartsWith("Wenn Sie Ihr Konto kündigen", refined[1].Preview);
    }

    [Fact]
    public async Task Numbers_the_question_already_names_do_not_pull_the_highlight()
    {
        var fragment = Fragment("The value is 2026 here. The value is 4400 here.");

        var refined = await Refiner.RefineAsync([fragment], "The value is 4400 for 2026 [1].", "The value for 2026?");

        Assert.Equal("The value is 4400 here.", Assert.Single(refined).Preview);
    }

    private const string Hours =
        "Steuerberatung Müller & Partner ist eine fiktive Kanzlei für dieses Demo-Projekt. " +
        "Am Samstag und Sonntag bleibt die Kanzlei geschlossen.";

    [Fact]
    public async Task The_question_helps_choose_between_sentences_that_share_the_answers_words()
    {
        var fragment = Fragment(Hours);

        var refined = await Refiner.RefineAsync([fragment],
            "Nein, die Steuerberatung Müller & Partner hat sonntags geschlossen [1].",
            "Ist die Kanzlei am Sonntag geschlossen?");

        Assert.Equal("Am Samstag und Sonntag bleibt die Kanzlei geschlossen.", Assert.Single(refined).Preview);
    }
}
