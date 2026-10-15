using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using Scrinium.Core.Sessions;
using Scrinium.Desktop.Services;
using Scrinium.Desktop.ViewModels;
using Scrinium.Ollama;

namespace Scrinium.Tests.Support;

public sealed class DesktopHarness : IDisposable
{
    private readonly TestApp _app;
    private readonly bool _ownsApp;

    public DesktopHarness(FakeChatClient? chat = null)
        : this(new TestApp(chat), ownsApp: true)
    {
    }

    private DesktopHarness(TestApp app, bool ownsApp)
    {
        _app = app;
        _ownsApp = ownsApp;
        var monitor = new OllamaMonitor(new OllamaApiClient(new Uri("http://127.0.0.1:1")), _app.Settings);
        Panel = new PanelHost(_app.Preview, _app.Settings, _app.Library, monitor, _app.Paths);
        Sources = new SourcesViewModel(_app.Library, _app.Settings, new NoPicker(), Panel);
        Chat = new ChatViewModel(_app.Answers, Panel, _app.Sessions, NullLogger<ChatViewModel>.Instance);
        Main = new MainViewModel(Sources, Chat, null!, Panel, _app.Settings);
    }

    public TestApp App => _app;

    public ISessionStore Sessions => _app.Sessions;

    public DesktopHarness Reopen() => new(_app, ownsApp: false);

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

    public void Dispose()
    {
        if (_ownsApp)
            _app.Dispose();
    }

    private sealed class NoPicker : IFilePicker
    {
        public Task<IReadOnlyList<string>> PickDocumentsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
