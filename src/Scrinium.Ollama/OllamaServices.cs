using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using Scrinium.Core.Settings;

namespace Scrinium.Ollama;

public static class OllamaServices
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddScriniumOllama(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri(sp.GetRequiredService<ISettingsProvider>().Current.OllamaUrl),
                Timeout = RequestTimeout
            };
            return new OllamaApiClient(http);
        });
        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<OllamaApiClient>());
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<OllamaApiClient>());
        services.AddSingleton<OllamaMonitor>();
        return services;
    }
}
