using Scrinium.Core.Models;

namespace Scrinium.Tests;

public class ModelChoicesTests
{
    private static readonly ModelCatalog Installed = new ModelCatalog.Available(
    [
        new InstalledModel("qwen3-coder:30b", CanChat: true, CanEmbed: false),
        new InstalledModel("bge-m3:latest", CanChat: false, CanEmbed: true),
        new InstalledModel("gemma3:12b", CanChat: true, CanEmbed: false)
    ]);

    [Fact]
    public void Chat_list_contains_only_models_that_can_chat()
    {
        var choices = ModelChoices.From(Installed, "gemma3:12b", "bge-m3:latest");

        Assert.Equal(["gemma3:12b", "qwen3-coder:30b"], choices.Chat.Order());
    }

    [Fact]
    public void Embedding_list_contains_only_embedding_models()
    {
        var choices = ModelChoices.From(Installed, "gemma3:12b", "bge-m3:latest");

        Assert.Equal(["bge-m3:latest"], choices.Embedding);
    }

    [Fact]
    public void A_model_that_does_both_appears_in_both_lists()
    {
        var catalog = new ModelCatalog.Available([new InstalledModel("all-rounder", CanChat: true, CanEmbed: true)]);

        var choices = ModelChoices.From(catalog, "all-rounder", "all-rounder");

        Assert.Equal(["all-rounder"], choices.Chat);
        Assert.Equal(["all-rounder"], choices.Embedding);
    }

    [Fact]
    public void A_configured_model_that_is_not_installed_stays_selectable_and_comes_first()
    {
        var choices = ModelChoices.From(Installed, "llama-not-installed", "bge-m3:latest");

        Assert.Equal("llama-not-installed", choices.Chat[0]);
        Assert.Contains("qwen3-coder:30b", choices.Chat);
    }

    [Fact]
    public void When_ollama_is_unavailable_only_the_configured_models_are_listed()
    {
        var choices = ModelChoices.From(new ModelCatalog.Unavailable(), "gemma3:12b", "bge-m3");

        Assert.Equal(["gemma3:12b"], choices.Chat);
        Assert.Equal(["bge-m3"], choices.Embedding);
    }

    [Fact]
    public void Models_are_not_listed_twice_when_the_configured_one_is_installed()
    {
        var choices = ModelChoices.From(Installed, "qwen3-coder:30b", "bge-m3:latest");

        Assert.Single(choices.Chat, name => name == "qwen3-coder:30b");
    }

    [Fact]
    public void A_configured_name_without_a_tag_matches_the_latest_tag()
    {
        var choices = ModelChoices.From(Installed, "gemma3:12b", "bge-m3");

        Assert.Equal(["bge-m3"], choices.Embedding);
    }

    [Fact]
    public void A_configured_latest_tag_matches_the_untagged_installed_name()
    {
        var catalog = new ModelCatalog.Available([new InstalledModel("bge-m3", CanChat: false, CanEmbed: true)]);

        var choices = ModelChoices.From(catalog, "gemma3:12b", "bge-m3:latest");

        Assert.Equal(["bge-m3:latest"], choices.Embedding);
    }
}
