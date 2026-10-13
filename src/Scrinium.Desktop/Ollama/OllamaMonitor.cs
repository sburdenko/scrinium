using OllamaSharp;
using Scrinium.Core.Settings;

namespace Scrinium.Desktop.Ollama;

public sealed class OllamaMonitor(OllamaApiClient client, ISettingsProvider settings)
{
    public async Task<OllamaStatus> CheckAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> installed;
        try
        {
            installed = (await client.ListLocalModelsAsync(ct)).Select(m => m.Name).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new OllamaStatus.Offline();
        }

        var current = settings.Current;
        var missing = new[] { current.EmbeddingModel, current.ChatModel }
            .Distinct()
            .Where(required => !installed.Any(name => Matches(name, required)))
            .ToList();

        return missing.Count == 0 ? new OllamaStatus.Ready() : new OllamaStatus.MissingModels(missing);
    }

    public async IAsyncEnumerable<string> PullAsync(
        string model,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var response in client.PullModelAsync(model, ct))
        {
            if (response is null)
                continue;

            yield return response.Total > 0
                ? $"{response.Status} · {response.Completed * 100 / response.Total}%"
                : response.Status;
        }
    }

    private static bool Matches(string installed, string required) =>
        installed.Equals(required, StringComparison.OrdinalIgnoreCase)
        || installed.Equals(required + ":latest", StringComparison.OrdinalIgnoreCase)
        || (!required.Contains(':') && installed.StartsWith(required + ":", StringComparison.OrdinalIgnoreCase));
}
