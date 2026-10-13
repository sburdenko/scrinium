using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core;
using Scrinium.Core.Chat;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Extraction;
using Scrinium.Ollama;
using Scrinium.Storage;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class CompositionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddScriniumCore(Path.Combine(_temp.Path, "data"), Path.Combine(_temp.Path, "settings.json"));
        services.AddScriniumSqliteStorage();
        services.AddScriniumTextExtractors();
        services.AddSingleton<IChatClient>(new FakeChatClient("ok"));
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new FakeEmbeddingGenerator());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void Every_registered_service_can_be_built()
    {
        using var provider = Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void The_entry_points_the_apps_use_resolve()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<AnswerService>());
        Assert.NotNull(provider.GetRequiredService<DocumentLibrary>());
        Assert.NotNull(provider.GetRequiredService<DocumentPreviewService>());
    }

    [Fact]
    public void Both_text_extractors_are_registered()
    {
        using var provider = Build();

        var extractors = provider.GetServices<Scrinium.Core.Extraction.ITextExtractor>().ToList();

        Assert.Equal(2, extractors.Count);
    }

    [Fact]
    public void The_Ollama_registration_provides_both_model_interfaces_and_the_monitor()
    {
        var services = new ServiceCollection();
        services.AddScriniumCore(Path.Combine(_temp.Path, "data"), Path.Combine(_temp.Path, "settings.json"));
        services.AddScriniumSqliteStorage();
        services.AddScriniumTextExtractors();
        services.AddScriniumOllama();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IChatClient>());
        Assert.NotNull(provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
        Assert.NotNull(provider.GetRequiredService<OllamaMonitor>());
    }
}
