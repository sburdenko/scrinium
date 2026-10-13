using Scrinium.Core.Chat;
using Scrinium.Core.Documents;
using Scrinium.Core.Retrieval;

namespace Scrinium.Tests;

public class PromptBuilderTests
{
    private static ContextFragment Fragment(int number, string text = "Some fragment text.") =>
        new(new Citation(number, Guid.NewGuid(), "doc.pdf", null, new DateOnly(2026, 1, 1), text,
                SourceLocation.Start(Guid.NewGuid())),
            text);

    private static string SystemPrompt(string question, params ContextFragment[] fragments) =>
        PromptBuilder.Build(question, fragments, [])[0].Text!;

    [Fact]
    public void System_prompt_states_how_many_fragments_there_are_and_their_numbers()
    {
        var prompt = SystemPrompt("question", Fragment(1), Fragment(2), Fragment(3));

        Assert.Contains("3 fragments", prompt);
        Assert.Contains("numbered 1 to 3", prompt);
    }

    [Fact]
    public void A_single_fragment_is_described_in_the_singular_and_the_example_uses_it()
    {
        var prompt = SystemPrompt("question", Fragment(1));

        Assert.Contains("1 fragment", prompt);
        Assert.DoesNotContain("1 fragments", prompt);
        Assert.Contains("for example [1]", prompt);
        Assert.DoesNotContain("for example [2]", prompt);
    }

    [Fact]
    public void The_language_reminder_comes_after_the_fragments()
    {
        var prompt = SystemPrompt("question", Fragment(1));

        var lastFragmentEnd = prompt.LastIndexOf("</fragment>", StringComparison.Ordinal);
        var reminder = prompt.IndexOf("language of the question", lastFragmentEnd, StringComparison.Ordinal);
        Assert.True(reminder > lastFragmentEnd);
    }

    [Fact]
    public void A_cyrillic_question_gets_an_explicit_language_hint()
    {
        var prompt = SystemPrompt("что за shopping app?", Fragment(1, "English fragment."));

        Assert.Contains("Cyrillic", prompt);
    }

    [Fact]
    public void A_latin_question_gets_no_cyrillic_hint()
    {
        var prompt = SystemPrompt("what is the shopping app?", Fragment(1));

        Assert.DoesNotContain("Cyrillic", prompt);
    }

    [Fact]
    public void The_system_prompt_explains_the_decline_marker()
    {
        var prompt = SystemPrompt("question", Fragment(1));

        Assert.Contains(PromptBuilder.DeclineMarker, prompt);
    }

    [Fact]
    public void The_question_is_followed_by_a_language_reminder()
    {
        var messages = PromptBuilder.Build("Wann haben Sie geöffnet?", [Fragment(1)], []);

        var last = messages[^1].Text!;
        Assert.StartsWith("Wann haben Sie geöffnet?", last);
        Assert.Contains("language of the question", last);
    }
}
