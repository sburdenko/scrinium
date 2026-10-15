using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Documents;
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
        Action<string>? seedDocuments = null,
        IReadOnlyDictionary<string, Action<string>>? workspaces = null)
    {
        var temp = new TempDirectory();
        var home = Path.Combine(temp.Path, "home");
        var documents = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(documents);
        seedDocuments?.Invoke(documents);

        var args = new List<string> { $"--Scrinium:Home={home}", $"--Scrinium:DocumentsFolder={documents}" };
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            args.Add($"--Scrinium:{key}={value}");

        var index = 0;
        foreach (var (id, seed) in workspaces ?? new Dictionary<string, Action<string>>())
        {
            var folder = Path.Combine(temp.Path, "documents-" + id);
            Directory.CreateDirectory(folder);
            seed(folder);
            args.Add($"--Scrinium:Workspaces:{index}:Id={id}");
            args.Add($"--Scrinium:Workspaces:{index}:DocumentsFolder={folder}");
            index++;
        }

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

    public async Task WaitUntilReadyAsync(int expectedDocuments, string? workspace = null)
    {
        var workspaces = Services.GetRequiredService<ServerWorkspaces>();
        var served = workspace is null
            ? workspaces.Default
            : workspaces.All.Single(w => w.Scope.Workspace.Id.Value == workspace);
        var library = served.Scope.Library;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var all = await library.ListAsync();
            if (all.Count >= expectedDocuments && all.All(d => d.State is IndexState.Ready or IndexState.Failed))
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException("Documents were not indexed in time.");
    }

    public async Task<IReadOnlyList<SseEvent>> ChatAsync(string question, object? history = null, string? workspace = null)
    {
        var path = workspace is null ? "/api/chat" : $"/w/{workspace}/api/chat";
        var response = await Client.PostAsJsonAsync(path, new { question, history });
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
