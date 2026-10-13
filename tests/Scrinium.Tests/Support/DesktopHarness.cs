using OllamaSharp;
using Scrinium.Desktop.Services;
using Scrinium.Desktop.ViewModels;
using Scrinium.Ollama;

namespace Scrinium.Tests.Support;

public sealed class DesktopHarness : IDisposable
{
    private readonly TestApp _app;

    public DesktopHarness(FakeChatClient? chat = null)
    {
        _app = new TestApp(chat);
        var monitor = new OllamaMonitor(new OllamaApiClient(new Uri("http://127.0.0.1:1")), _app.Settings);
        Panel = new PanelHost(_app.Preview, _app.Settings, _app.Library, monitor, _app.Paths);
        Sources = new SourcesViewModel(_app.Library, _app.Settings, new NoPicker(), Panel);
        Chat = new ChatViewModel(_app.Answers, Panel);
        Main = new MainViewModel(Sources, Chat, null!, Panel);
    }

    public TestApp App => _app;

    public PanelHost Panel { get; }

    public SourcesViewModel Sources { get; }

    public ChatViewModel Chat { get; }

    public MainViewModel Main { get; }

    public async Task<Scrinium.Core.Documents.Document> AddDocumentAsync(string name, string text)
    {
        var document = await _app.ImportAndProcessAsync(_app.WriteFile(name, text));
        await Sources.InitializeAsync();
        return document;
    }

    public async Task AskAsync(string question)
    {
        Chat.Input = question;
        await Chat.SendCommand.ExecuteAsync(null);
    }

    public void Dispose() => _app.Dispose();

    private sealed class NoPicker : IFilePicker
    {
        public Task<IReadOnlyList<string>> PickDocumentsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
