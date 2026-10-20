using Scrinium.Core.Retrieval;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class RetrievalTraceTests : IDisposable
{
    private readonly List<TestApp> _apps = [];

    public void Dispose() => _apps.ForEach(a => a.Dispose());

    private TestApp NewApp(RetrievalOptions? options = null)
    {
        var app = new TestApp(retrieval: options);
        _apps.Add(app);
        return app;
    }

    private async Task<TestApp> StrongAndWeakAsync(RetrievalOptions options)
    {
        var app = NewApp(options);
        await app.ImportAndProcessAsync(app.WriteFile("strong.txt", "alpha beta gamma delta"));
        await app.ImportAndProcessAsync(app.WriteFile("weak.txt", "alpha epsilon zeta eta theta iota"));
        return app;
    }

    [Fact]
    public async Task The_trace_names_the_query_and_the_settings_it_ran_with()
    {
        var options = new RetrievalOptions(MinScore: 0.1, ScoreMargin: 0.3);
        var app = await StrongAndWeakAsync(options);

        var outcome = await app.Retriever.RetrieveAsync("alpha beta gamma delta");

        Assert.Equal("alpha beta gamma delta", outcome.Trace.Query);
        Assert.Equal(options, outcome.Trace.Options);
    }

    [Fact]
    public async Task A_hit_far_below_the_best_one_is_traced_as_outside_the_margin()
    {
        var app = await StrongAndWeakAsync(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 0.3));

        var outcome = await app.Retriever.RetrieveAsync("alpha beta gamma delta");

        Assert.Equal(
            [("strong.txt", CandidateFate.Sent), ("weak.txt", CandidateFate.OutsideMargin)],
            outcome.Trace.Candidates.Select(c => (c.DocumentTitle, c.Fate)));
        Assert.All(outcome.Trace.Candidates, c => Assert.Equal(CandidateSource.Meaning, c.Source));
    }

    [Fact]
    public async Task Candidates_are_listed_from_the_highest_score_down()
    {
        var app = await StrongAndWeakAsync(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 1.0));

        var outcome = await app.Retriever.RetrieveAsync("alpha beta gamma delta");

        var scores = outcome.Trace.Candidates.Select(c => c.Score).ToList();
        Assert.Equal(scores.OrderDescending(), scores);
        Assert.True(scores[0] > scores[1]);
    }

    [Fact]
    public async Task A_question_below_the_minimum_score_traces_every_candidate_as_below_it()
    {
        var app = NewApp();
        await app.ImportAndProcessAsync(app.WriteFile("motor.md", "Звук мотора стал громче."));

        var outcome = await app.Retriever.RetrieveAsync("рыжий кот любит молоко");

        var nothing = Assert.IsType<RetrievalOutcome.NothingRelevant>(outcome);
        var candidate = Assert.Single(nothing.Trace.Candidates);
        Assert.Equal(("motor.md", CandidateFate.BelowMinScore, nothing.BestScore), (candidate.DocumentTitle, candidate.Fate, candidate.Score));
    }

    [Fact]
    public async Task An_empty_archive_traces_no_candidates()
    {
        var outcome = await NewApp().Retriever.RetrieveAsync("любой вопрос");

        Assert.Empty(Assert.IsType<RetrievalOutcome.ArchiveEmpty>(outcome).Trace.Candidates);
    }

    [Fact]
    public async Task A_hit_that_does_not_fit_the_token_budget_is_traced_as_over_budget()
    {
        var app = await StrongAndWeakAsync(new RetrievalOptions(MinScore: 0.1, ScoreMargin: 1.0, ContextTokenBudget: 1));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("alpha beta gamma delta"));

        Assert.Single(found.Fragments);
        Assert.Equal(
            [("strong.txt", CandidateFate.Sent), ("weak.txt", CandidateFate.OverBudget)],
            found.Trace.Candidates.Select(c => (c.DocumentTitle, c.Fate)));
    }

    [Fact]
    public async Task A_code_found_by_its_words_is_traced_as_a_word_hit_and_the_hit_it_pushed_out_as_beyond_top_k()
    {
        var app = NewApp(new RetrievalOptions(TopK: 2, MinScore: 0.1, ScoreMargin: 0.1));
        await app.ImportAndProcessAsync(app.WriteFile("faq-1.txt", "What does the error mean for you"));
        await app.ImportAndProcessAsync(app.WriteFile("faq-2.txt", "What does this error mean today"));
        await app.ImportAndProcessAsync(app.WriteFile("faq-3.txt", "Error meaning: what does it mean"));
        await app.ImportAndProcessAsync(app.WriteFile("codes.txt", "E507 destination disk full"));

        var found = Assert.IsType<RetrievalOutcome.Found>(await app.Retriever.RetrieveAsync("What does error E507 mean?"));

        var code = Assert.Single(found.Trace.Candidates, c => c.DocumentTitle == "codes.txt");
        Assert.Equal((CandidateSource.Words, CandidateFate.Sent), (code.Source, code.Fate));
        Assert.Contains(found.Trace.Candidates, c => c.Fate == CandidateFate.BeyondTopK);
        Assert.Equal(found.Fragments.Count, found.Trace.Candidates.Count(c => c.Fate == CandidateFate.Sent));
    }
}
