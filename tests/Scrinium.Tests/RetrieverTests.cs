using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class RetrieverTests : IDisposable
{
    private readonly List<TestApp> _apps = [];

    public void Dispose() => _apps.ForEach(a => a.Dispose());

    private TestApp NewApp(RetrievalOptions? options = null)
    {
        var app = new TestApp(retrieval: options);
        _apps.Add(app);
        return app;
    }

    [Fact]
    public async Task Empty_archive_is_reported_as_archive_empty()
    {
        var app = NewApp();

        var outcome = await app.Retriever.RetrieveAsync("любой вопрос");

        Assert.IsType<RetrievalOutcome.ArchiveEmpty>(outcome);
    }

    [Fact]
    public async Task Unrelated_question_is_nothing_relevant_with_the_closest_hit()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("motor.md", "Звук мотора стал громче."));

        var outcome = await app.Retriever.RetrieveAsync("рыжий кот любит молоко");

        var nothing = Assert.IsType<RetrievalOutcome.NothingRelevant>(outcome);
        Assert.Equal("motor.md", nothing.ClosestTitle);
        Assert.True(nothing.BestScore < new RetrievalOptions().MinScore);
    }

    [Fact]
    public async Task Only_documents_above_the_threshold_are_returned()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("motor.md", "Звук мотора стал громче."));
        await app.ImportAndProcessAsync(app.WriteFile("rent.txt", "Аренда квартиры продлена до марта."));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("звук мотора"));

        Assert.Equal("motor.md", Assert.Single(found.Fragments).Citation.DocumentTitle);
    }

    [Fact]
    public async Task Hits_far_below_the_best_one_are_dropped_by_the_margin()
    {
        var app = NewApp(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 0.3));
        await app.ImportAndProcessAsync(app.WriteFile("strong.txt", "alpha beta gamma delta"));
        await app.ImportAndProcessAsync(app.WriteFile("weak.txt", "alpha epsilon zeta eta theta iota"));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("alpha beta gamma delta"));

        Assert.Equal("strong.txt", Assert.Single(found.Fragments).Citation.DocumentTitle);
    }

    [Fact]
    public async Task A_wide_margin_keeps_weaker_hits()
    {
        var app = NewApp(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 1.0));
        await app.ImportAndProcessAsync(app.WriteFile("strong.txt", "alpha beta gamma delta"));
        await app.ImportAndProcessAsync(app.WriteFile("weak.txt", "alpha epsilon zeta eta theta iota"));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("alpha beta gamma delta"));

        Assert.Equal(["strong.txt", "weak.txt"], found.Fragments.Select(f => f.Citation.DocumentTitle));
    }

    [Fact]
    public async Task Citation_numbers_follow_relevance_order_from_one()
    {
        var app = NewApp(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 1.0));
        await app.ImportAndProcessAsync(app.WriteFile("weak.txt", "alpha epsilon zeta eta theta iota"));
        await app.ImportAndProcessAsync(app.WriteFile("strong.txt", "alpha beta gamma delta"));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("alpha beta gamma delta"));

        Assert.Equal([1, 2], found.Fragments.Select(f => f.Citation.Number));
        Assert.Equal("strong.txt", found.Fragments[0].Citation.DocumentTitle);
    }

    [Fact]
    public async Task A_loosely_related_question_still_reaches_the_model()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("notes.txt", "alpha beta gamma delta epsilon zeta"));

        var outcome = await app.Retriever.RetrieveAsync("alpha beta xx");

        var found = Assert.IsType<RetrievalOutcome.Found>(outcome);
        Assert.Equal("notes.txt", Assert.Single(found.Fragments).Citation.DocumentTitle);
    }

    [Fact]
    public async Task Citation_points_to_the_sentence_that_best_matches_the_question()
    {
        var app = NewApp();
        const string text = "The invoice total was paid in September. The bearing makes a grinding noise on cold starts. The tyres are changed every November.";
        await app.ImportAndProcessAsync(app.WriteFile("car.txt", text));

        var found = Assert.IsType<RetrievalOutcome.Found>(
            await app.Retriever.RetrieveAsync("bearing grinding noise cold starts"));

        var citation = Assert.Single(found.Fragments).Citation;
        const string expected = "The bearing makes a grinding noise on cold starts.";
        Assert.Equal(text.IndexOf(expected, StringComparison.Ordinal), citation.Location.Offset);
        Assert.Equal(expected.Length, citation.Location.Length);
        Assert.Equal(expected, citation.Preview);
    }

    [Fact]
    public async Task The_prompt_still_gets_the_whole_chunk()
    {
        var app = NewApp();
        const string text = "The invoice total was paid in September. The bearing makes a grinding noise on cold starts. The tyres are changed every November.";
        await app.ImportAndProcessAsync(app.WriteFile("car.txt", text));

        var found = Assert.IsType<RetrievalOutcome.Found>(
            await app.Retriever.RetrieveAsync("bearing grinding noise cold starts"));

        Assert.Equal(text, Assert.Single(found.Fragments).PromptText);
    }

    [Fact]
    public async Task A_fragment_carries_the_section_of_its_chunk()
    {
        var app = NewApp();
        var filler = string.Join(" ", Enumerable.Range(1, 30).Select(i => $"Line {i} describes the plan in general terms."));
        await app.ImportAndProcessAsync(app.WriteFile("prices.md",
            $"# Prices\n{filler} The walrus discount is ten percent for annual billing."));

        var outcome = await app.Retriever.RetrieveAsync("walrus discount annual billing");

        Assert.Equal("Prices", Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments[0].Section);
    }

    [Fact]
    public async Task A_fragment_says_when_its_chunk_is_code()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("install.md",
            "# Install\n\nThe installer needs Node.\n\n```bash\nnpm install walrus-cli --global\n```"));

        var outcome = await app.Retriever.RetrieveAsync("npm install walrus-cli --global");

        var fragment = Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments[0];
        Assert.Equal(ChunkContent.Code, fragment.Content);
        Assert.Equal("Install", fragment.Section);
    }

    private static async Task<TestApp> ArchiveWithAnErrorCodeAsync(TestApp app)
    {
        await app.ImportAndProcessAsync(app.WriteFile("faq-1.txt", "What does the error mean for you"));
        await app.ImportAndProcessAsync(app.WriteFile("faq-2.txt", "What does this error mean today"));
        await app.ImportAndProcessAsync(app.WriteFile("faq-3.txt", "Error meaning: what does it mean"));
        await app.ImportAndProcessAsync(app.WriteFile("codes.txt", "E507 destination disk full"));
        return app;
    }

    private static readonly RetrievalOptions TwoHitsLowFloor = new(TopK: 2, MinScore: 0.1, ScoreMargin: 0.1);

    [Fact]
    public async Task A_code_that_meaning_search_ranks_too_low_is_found_by_its_words()
    {
        var app = await ArchiveWithAnErrorCodeAsync(NewApp(TwoHitsLowFloor));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("What does error E507 mean?"));

        Assert.Contains(found.Fragments, f => f.Citation.DocumentTitle == "codes.txt");
        Assert.True(found.Fragments.Count <= 2);
    }

    [Fact]
    public async Task Meaning_search_alone_does_not_see_the_code()
    {
        var app = await ArchiveWithAnErrorCodeAsync(NewApp(TwoHitsLowFloor with { Search = SearchMode.Meaning }));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("What does error E507 mean?"));

        Assert.DoesNotContain(found.Fragments, f => f.Citation.DocumentTitle == "codes.txt");
    }

    [Fact]
    public async Task A_question_without_identifiers_retrieves_what_meaning_search_retrieves()
    {
        var hybrid = await ArchiveWithAnErrorCodeAsync(NewApp(TwoHitsLowFloor));
        var meaning = await ArchiveWithAnErrorCodeAsync(NewApp(TwoHitsLowFloor with { Search = SearchMode.Meaning }));

        var fromHybrid = Assert.IsType<RetrievalOutcome.Found>(await hybrid.Retriever.RetrieveAsync("what does the error mean"));
        var fromMeaning = Assert.IsType<RetrievalOutcome.Found>(await meaning.Retriever.RetrieveAsync("what does the error mean"));

        Assert.Equal(fromMeaning.Fragments.Select(f => f.Citation.DocumentTitle), fromHybrid.Fragments.Select(f => f.Citation.DocumentTitle));
    }

    [Fact]
    public async Task A_word_match_does_not_rescue_a_question_whose_meaning_is_unrelated()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("pasta.txt", "E507 is the code on the jar of tomatoes we used for pasta with basil and garlic tonight"));

        var outcome = await app.Retriever.RetrieveAsync("E507?");

        Assert.IsType<RetrievalOutcome.NothingRelevant>(outcome);
    }

    private static string LongNote()
    {
        var sentences = Enumerable.Range(1, 40).Select(i => $"Sentence number {i} talks about topic {i} in some detail here.");
        return string.Join(" ", sentences) + " The walrus fact is that walruses sleep on ice floes.";
    }

    [Fact]
    public async Task The_window_around_a_hit_reaches_into_its_neighbours()
    {
        var app = NewApp();
        var text = LongNote();
        await app.ImportAndProcessAsync(app.WriteFile("note.txt", text));

        var outcome = await app.Retriever.RetrieveAsync("walrus walruses sleep ice floes");

        var fragment = Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments[0];
        Assert.Contains("The walrus fact is that walruses sleep on ice floes.", fragment.ChunkText);
        Assert.Contains("Sentence number", fragment.ChunkText);
    }

    [Fact]
    public async Task Every_position_in_the_window_maps_back_to_the_same_text_in_the_document()
    {
        var app = NewApp();
        var text = LongNote();
        await app.ImportAndProcessAsync(app.WriteFile("note.txt", text));

        var outcome = await app.Retriever.RetrieveAsync("walrus walruses sleep ice floes");

        var fragment = Assert.IsType<RetrievalOutcome.Found>(outcome).Fragments[0];
        Assert.Equal(text.Substring(fragment.ChunkOffset, fragment.ChunkText.Length), fragment.ChunkText);
        var cited = fragment.Citation.Location;
        Assert.Equal(fragment.Citation.Preview, text.Substring(cited.Offset, cited.Length));
    }
}
