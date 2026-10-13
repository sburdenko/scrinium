using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Chat;
using Scrinium.Core.Chunking;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core;

public static class ScriniumServices
{
    public static IServiceCollection AddScriniumCore(
        this IServiceCollection services,
        string dataDirectory,
        string settingsFilePath)
    {
        services.AddSingleton(new DataPaths(dataDirectory));
        services.AddSingleton(new SettingsService(settingsFilePath));
        services.AddSingleton<ISettingsProvider>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(new TextChunker(new ChunkingOptions()));
        services.AddSingleton(new RetrievalOptions());

        services.AddSingleton<IngestionPipeline>();
        services.AddSingleton<DocumentLibrary>();
        services.AddHostedService<IngestionWorkerService>();
        services.AddSingleton<PassageLocator>();
        services.AddSingleton<Retriever>();
        services.AddSingleton<AnswerService>();
        services.AddSingleton<DocumentPreviewService>();

        return services;
    }
}
