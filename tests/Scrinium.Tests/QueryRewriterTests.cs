using Scrinium.Core.Chat;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class QueryRewriterTests
{
    private static IReadOnlyList<ChatTurn> History =>
    [
        ChatTurn.User("How much does the Business plan cost per month?"),
        ChatTurn.Assistant("The Business plan costs 199 euros per month [1].")
    ];

    [Fact]
    public async Task Without_history_the_question_is_used_and_the_model_is_not_called()
    {
        using var app = new TestApp();

        var query = await app.Rewriter.RewriteAsync("What does Team cost?", []);

        Assert.Equal("What does Team cost?", query);
        Assert.Empty(app.Chat.NonStreamingRequests);
    }

    [Fact]
    public async Task With_history_the_model_makes_the_question_standalone()
    {
        using var app = new TestApp();
        app.Chat.NonStreamingReply = "How much does the Team plan cost per month?";

        var query = await app.Rewriter.RewriteAsync("And Team?", History);

        Assert.Equal("How much does the Team plan cost per month?", query);
        var request = Assert.Single(app.Chat.NonStreamingRequests);
        Assert.Contains(request, m => m.Text!.Contains("Business plan costs 199"));
        Assert.Contains("And Team?", request[^1].Text);
    }

    [Fact]
    public async Task Quotes_and_whitespace_around_the_rewritten_query_are_removed()
    {
        using var app = new TestApp();
        app.Chat.NonStreamingReply = "  \"What does Team cost?\"\n";

        var query = await app.Rewriter.RewriteAsync("And Team?", History);

        Assert.Equal("What does Team cost?", query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_rewrite_falls_back_to_the_question(string reply)
    {
        using var app = new TestApp();
        app.Chat.NonStreamingReply = reply;

        var query = await app.Rewriter.RewriteAsync("And Team?", History);

        Assert.Equal("And Team?", query);
    }

    [Fact]
    public async Task A_rewrite_that_rambles_falls_back_to_the_question()
    {
        using var app = new TestApp();
        app.Chat.NonStreamingReply = new string('x', 1000);

        var query = await app.Rewriter.RewriteAsync("And Team?", History);

        Assert.Equal("And Team?", query);
    }

    [Fact]
    public async Task A_follow_up_finds_its_fragment_through_the_rewritten_query()
    {
        using var app = new TestApp(new FakeChatClient("It costs 79 euros [1]."));
        await app.ImportAndProcessAsync(app.WriteFile("plans.txt", "The Team plan costs 79 euros per month."));
        app.Chat.NonStreamingReply = "How much does the Team plan cost?";

        var events = new List<AnswerEvent>();
        await foreach (var e in app.Answers.AskAsync("And Team?", History))
            events.Add(e);

        var sources = events.OfType<AnswerEvent.SourcesFound>().First();
        Assert.Equal("plans.txt", Assert.Single(sources.Citations).DocumentTitle);
    }

    [Fact]
    public async Task Without_the_rewrite_the_same_follow_up_finds_nothing()
    {
        using var app = new TestApp(new FakeChatClient("It costs 79 euros [1]."));
        await app.ImportAndProcessAsync(app.WriteFile("plans.txt", "The Team plan costs 79 euros per month."));

        var events = new List<AnswerEvent>();
        await foreach (var e in app.Answers.AskAsync("And Team?", []))
            events.Add(e);

        Assert.Empty(events.OfType<AnswerEvent.SourcesFound>().First().Citations);
    }
}
