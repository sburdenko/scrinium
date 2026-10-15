using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class ServerWorkspaceTests
{
    private const string EngineNote = "Engine noise got louder.";
    private const string RateNote = "The Team plan allows 1200 requests per minute.";

    private static Action<string> Seed(string name, string text) =>
        folder => File.WriteAllText(Path.Combine(folder, name), text);

    private static async Task<ServerHarness> StartTwoCompaniesAsync(IReadOnlyDictionary<string, string>? settings = null)
    {
        var server = await ServerHarness.StartAsync(
            settings,
            seedDocuments: Seed("engine.txt", EngineNote),
            workspaces: new Dictionary<string, Action<string>> { ["kestrel"] = Seed("rates.txt", RateNote) });
        await server.WaitUntilReadyAsync(1);
        await server.WaitUntilReadyAsync(1, "kestrel");
        return server;
    }

    private static HttpRequestMessage WithKey(string path, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", key);
        return request;
    }

    [Fact]
    public async Task Each_workspace_answers_only_from_its_own_documents()
    {
        await using var server = await StartTwoCompaniesAsync();

        var kestrel = await server.ChatAsync("engine noise louder", workspace: "kestrel");
        var main = await server.ChatAsync("engine noise louder");

        var kestrelTitles = kestrel.Where(e => e.Type == "sources")
            .SelectMany(e => e.Json.EnumerateArray()).Select(s => s.GetProperty("title").GetString());
        Assert.DoesNotContain("engine.txt", kestrelTitles);
        Assert.Equal("engine.txt", main.First(e => e.Type == "sources").Json[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_unknown_workspace_is_not_found()
    {
        await using var server = await StartTwoCompaniesAsync();

        var chat = await server.Client.PostAsJsonAsync("/w/nobody/api/chat", new { question = "engine" });
        var page = await server.Client.GetAsync("/w/nobody/");
        var invalid = await server.Client.GetAsync("/w/..%2F..%2Fetc/api/health");

        Assert.Equal(HttpStatusCode.NotFound, chat.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
    }

    [Fact]
    public async Task A_source_of_one_workspace_cannot_be_read_through_another()
    {
        await using var server = await StartTwoCompaniesAsync();
        var main = await server.ChatAsync("engine noise louder");
        var source = main.First(e => e.Type == "sources").Json[0];
        var query = $"{source.GetProperty("documentId").GetGuid()}?offset=0&length=5";

        var own = await server.Client.GetAsync($"/api/sources/{query}");
        var foreign = await server.Client.GetAsync($"/w/kestrel/api/sources/{query}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Each_workspace_has_its_own_api_key()
    {
        await using var server = await StartTwoCompaniesAsync(new Dictionary<string, string>
        {
            ["ApiKey"] = "main-key",
            ["Workspaces:0:ApiKey"] = "kestrel-key"
        });

        var wrongKey = await server.Client.SendAsync(WithKey("/w/kestrel/api/documents", "main-key"));
        var rightKey = await server.Client.SendAsync(WithKey("/w/kestrel/api/documents", "kestrel-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rightKey.StatusCode);
        var titles = (await rightKey.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Select(d => d.GetProperty("title").GetString());
        Assert.Equal(["rates.txt"], titles);
    }

    [Fact]
    public async Task Health_counts_the_documents_of_the_requested_workspace()
    {
        await using var server = await StartTwoCompaniesAsync();

        var kestrel = await server.Client.GetFromJsonAsync<JsonElement>("/w/kestrel/api/health");

        Assert.Equal(1, kestrel.GetProperty("documents").GetInt32());
    }

    [Fact]
    public async Task The_chat_page_is_served_for_a_workspace_and_loads_its_assets_from_the_root()
    {
        await using var server = await StartTwoCompaniesAsync();

        var response = await server.Client.GetAsync("/w/kestrel/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("src=\"/chat.js\"", html);
        Assert.Contains("href=\"/chat.css\"", html);
    }

    [Fact]
    public async Task An_invalid_workspace_id_in_the_configuration_stops_the_server_from_starting()
    {
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => ServerHarness.StartAsync(
            new Dictionary<string, string> { ["Workspaces:0:Id"] = "../escape" }));

        Assert.Contains("invalid", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
