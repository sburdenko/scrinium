using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Chunking;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;
using Scrinium.Core.Workspaces;

namespace Scrinium.Core;

public static class ScriniumServices
{
    public static IServiceCollection AddScriniumCore(
        this IServiceCollection services,
        string dataDirectory,
        string settingsFilePath)
    {
        services.AddSingleton(new SettingsService(settingsFilePath));
        services.AddSingleton<ISettingsProvider>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(new TextChunker(new ChunkingOptions()));
        services.AddSingleton(new RetrievalOptions());

        services.AddSingleton(new WorkspaceCatalog(dataDirectory));
        services.AddSingleton<WorkspaceScopeFactory>();

        return services;
    }
}
