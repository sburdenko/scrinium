using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core;
using Scrinium.Core.Workspaces;
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

        var catalog = provider.GetRequiredService<WorkspaceCatalog>();
        var scope = provider.GetRequiredService<WorkspaceScopeFactory>().Open(catalog.EnsureDefault("Personal"));

        Assert.NotNull(scope.Answers);
        Assert.NotNull(scope.Library);
        Assert.NotNull(scope.Previews);
    }

    [Fact]
    public void Every_document_kind_has_exactly_one_registered_reader()
    {
        using var provider = Build();

        var extractors = provider.GetServices<Scrinium.Core.Extraction.ITextExtractor>().ToList();

        Assert.All(Enum.GetValues<Scrinium.Core.Documents.DocumentKind>(), kind =>
            Assert.Single(extractors, e => e.Supports(kind)));
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
