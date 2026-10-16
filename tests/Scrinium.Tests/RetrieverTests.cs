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
