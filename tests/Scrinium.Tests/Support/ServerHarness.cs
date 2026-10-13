using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Server;

namespace Scrinium.Tests.Support;

public sealed class ServerHarness : IAsyncDisposable
{
    private readonly TempDirectory _temp;
    private readonly WebApplication _app;

    private ServerHarness(TempDirectory temp, WebApplication app, HttpClient client, FakeChatClient chat, string home, string documents)
    {
        _temp = temp;
        _app = app;
        Client = client;
        Chat = chat;
        Home = home;
        DocumentsFolder = documents;
    }

    public HttpClient Client { get; }

    public FakeChatClient Chat { get; }

    public string Home { get; }

    public string DocumentsFolder { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<ServerHarness> StartAsync(
        IReadOnlyDictionary<string, string>? settings = null,
        string[]? answerTokens = null,
        Action<string>? seedDocuments = null)
    {
        var temp = new TempDirectory();
        var home = Path.Combine(temp.Path, "home");
        var documents = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(documents);
        seedDocuments?.Invoke(documents);

        var args = new List<string> { $"--Scrinium:Home={home}", $"--Scrinium:DocumentsFolder={documents}" };
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            args.Add($"--Scrinium:{key}={value}");

        var chat = new FakeChatClient(answerTokens ?? ["Louder ", "in September ", "[1]."]);
        var app = ServerApp.Build(args.ToArray(), builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IChatClient>(chat);
            builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new FakeEmbeddingGenerator());
        });

        await app.StartAsync();
        return new ServerHarness(temp, app, app.GetTestClient(), chat, home, documents);
    }

    public async Task WaitUntilReadyAsync(int expectedDocuments)
    {
        var library = Services.GetRequiredService<DocumentLibrary>();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var all = await library.ListAsync();
            if (all.Count >= expectedDocuments && all.All(d => d.State is IndexState.Ready or IndexState.Failed))
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException("Documents were not indexed in time.");
    }

    public async Task<IReadOnlyList<SseEvent>> ChatAsync(string question, object? history = null)
    {
        var response = await Client.PostAsJsonAsync("/api/chat", new { question, history });
        response.EnsureSuccessStatusCode();
        return SseEvent.Parse(await response.Content.ReadAsStringAsync());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _temp.Dispose();
    }
}

public sealed record SseEvent(string Type, string Data)
{
    public JsonElement Json => JsonDocument.Parse(Data).RootElement;

    public static IReadOnlyList<SseEvent> Parse(string body)
    {
        var events = new List<SseEvent>();
        foreach (var block in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var type = "message";
            var data = new StringBuilder();
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                    type = line[6..].Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                    data.Append(line[5..].TrimStart());
            }

            events.Add(new SseEvent(type, data.ToString()));
        }

        return events;
    }
}
