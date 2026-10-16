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
            "Wenn Sie Ihr Konto kündigen, können Sie Ihre Daten noch 30 Tage nach Vertragsende exportieren. " +
            "Danach werden alle Daten endgültig gelöscht.",
            Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_neighbouring_sentence_the_answer_does_not_restate_is_left_out()
    {
        var fragment = Fragment(
            "Monthly plans can be cancelled at any time. Annual plans are refunded in full when cancelled within the first 30 days.");

        var refined = await Refiner.RefineAsync([fragment],
            "Annual plans are refunded in full when cancelled within the first 30 days [1].", "Can I get a refund?");

        Assert.Equal("Annual plans are refunded in full when cancelled within the first 30 days.", Assert.Single(refined).Preview);
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

    [Fact]
    public async Task A_short_answer_is_placed_by_the_question()
    {
        var fragment = Fragment("Montags bis freitags ist die Kanzlei von 8:30 bis 17 Uhr geöffnet. Am Sonntag bleibt die Kanzlei geschlossen.");

        var refined = await Refiner.RefineAsync([fragment], "Ja [1].", "Ist die Kanzlei am Sonntag geschlossen?");

        Assert.Equal("Am Sonntag bleibt die Kanzlei geschlossen.", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_sentence_with_the_claimed_number_wins_over_a_longer_sentence_that_sounds_alike()
    {
        var fragment = Fragment(
            "With annual billing the Business plan costs 1,910.40 euros per year and the Team plan costs 758.40 euros per year.\n" +
            "| Business | 199 |");

        var refined = await Refiner.RefineAsync([fragment],
            "The Business plan costs 199 euros per month [1].", "What does the Business plan cost per month?");

        Assert.Equal("| Business | 199 |", Assert.Single(refined).Preview);
    }

    private const string Faq = "## Kann ich Unterlagen digital einreichen?\n\nJa. Nutzen Sie das Mandantenportal oder senden Sie die Dateien verschlüsselt.";

    [Fact]
    public async Task A_heading_that_repeats_the_question_is_not_the_evidence()
    {
        var fragment = Fragment(Faq);

        var refined = await Refiner.RefineAsync([fragment],
            "Ja, Sie können Unterlagen digital einreichen [1].", "Kann ich Unterlagen digital einreichen?");

        Assert.StartsWith("Ja. Nutzen Sie das Mandantenportal", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task A_heading_that_holds_the_stated_date_is_the_evidence()
    {
        var fragment = Fragment("## Version 3.4, released on 12 September 2025\n\nAdded single sign-on with SAML 2.0 for the Business plan.");

        var refined = await Refiner.RefineAsync([fragment],
            "Single sign-on was added on 12 September 2025 [1].", "When was single sign-on added?");

        Assert.Equal("## Version 3.4, released on 12 September 2025", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task When_a_cited_fragment_lacks_the_stated_number_the_highlight_moves_to_the_fragment_that_holds_it()
    {
        var wrong = Fragment("The POST /events endpoint accepts a batch of up to 1,000 events in one request.", offset: 0, number: 1);
        var right = Fragment(Chunk, offset: 200, number: 2);

        var refined = await Refiner.RefineAsync([wrong, right],
            "The Team plan allows 1,200 requests per minute [1].", "What about Team?");

        Assert.Equal("| Team | 1,200 |", refined[0].Preview);
        Assert.Equal(right.Citation.DocumentId, refined[0].DocumentId);
    }

    [Fact]
    public async Task The_first_claim_with_a_figure_decides_when_a_source_is_cited_twice()
    {
        var fragment = Fragment("| Team | 79 |\n| Business | 199 |");

        var refined = await Refiner.RefineAsync([fragment],
            "The cheapest plan is Team at 79 euros per month [1]. It is cheaper than Business at 199 euros per month [1].",
            "What is the cheapest plan?");

        Assert.Equal("| Team | 79 |", Assert.Single(refined).Preview);
    }

    [Fact]
    public async Task Adjacent_sentences_on_the_same_line_are_highlighted_together_when_the_answer_states_both()
    {
        var fragment = Fragment(
            "Monthly plans can be cancelled at any time. Annual plans are refunded in full when cancelled within the first 30 days. After that, the remaining months are not refunded.");

        var refined = await Refiner.RefineAsync([fragment],
            "Annual plans are refunded in full when cancelled within the first 30 days. After that, the remaining months are not refunded [1].",
            "Can I get a refund?");

        Assert.Equal(
            "Annual plans are refunded in full when cancelled within the first 30 days. After that, the remaining months are not refunded.",
            Assert.Single(refined).Preview);
    }


    [Fact]
    public async Task Sentences_that_share_no_word_or_number_with_the_claim_are_not_sent_to_the_model()
    {
        var unrelated = string.Join(" ", Enumerable.Range(1, 10).Select(i => $"Lorem ipsum dolor sit amet number {i * 1000} consectetur."));
        var fragment = Fragment(unrelated + " Walruses sleep on floating ice.");
        var before = _app.Embedder.Texts;

        var refined = await Refiner.RefineAsync([fragment], "Walruses sleep on ice [1].", "Where do walruses sleep?");

        Assert.Equal("Walruses sleep on floating ice.", Assert.Single(refined).Preview);
        Assert.True(_app.Embedder.Texts - before <= 4, $"embedded {_app.Embedder.Texts - before} texts");
    }

    [Fact]
    public async Task When_no_sentence_shares_a_word_with_the_claim_every_sentence_is_compared()
    {
        var fragment = Fragment("Beide Seiten können mit einer Frist von vier Wochen kündigen. Die Kanzlei liegt in Bonn.");

        var refined = await Refiner.RefineAsync([fragment], "Расторгнуть можно за четыре недели [1].", "Какой срок расторжения?");

        Assert.Single(refined);
        Assert.NotEqual("initial", refined[0].Preview);
    }

    private sealed class MeaningEmbedder(Dictionary<string, float[]> meanings) : Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>
    {
        public Task<Microsoft.Extensions.AI.GeneratedEmbeddings<Microsoft.Extensions.AI.Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, Microsoft.Extensions.AI.EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Microsoft.Extensions.AI.GeneratedEmbeddings<Microsoft.Extensions.AI.Embedding<float>>(
                values.Select(v => new Microsoft.Extensions.AI.Embedding<float>(meanings.GetValueOrDefault(v, [0f, 0f, 1f]))).ToList()));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    [Fact]
    public async Task The_first_guess_stays_a_candidate_even_when_it_shares_no_word_with_the_answer()
    {
        const string echo = "Unterlagen bitte vollständig einreichen.";
        const string answer = "Nutzen Sie das Mandantenportal oder eine verschlüsselte E-Mail.";
        const string claim = "Ja, Sie können Unterlagen digital einreichen";
        var embedder = new MeaningEmbedder(new()
        {
            [claim] = [1f, 0f, 0f],
            [answer] = [1f, 0f, 0f],
            [echo] = [0f, 1f, 0f]
        });
        var chunk = echo + " " + answer;
        var id = Guid.NewGuid();
        var firstGuess = new Citation(1, id, "faq.md", null, new DateOnly(2026, 1, 1), answer,
            new SourceLocation(id, null, 100 + echo.Length + 1, answer.Length));
        var fragment = new ContextFragment(firstGuess, chunk, chunk, 100);

        var refined = await new PassageRefiner(embedder, _app.Settings).RefineAsync([fragment], claim + " [1].", "");

        Assert.Contains(answer, Assert.Single(refined).Preview);
    }
}
