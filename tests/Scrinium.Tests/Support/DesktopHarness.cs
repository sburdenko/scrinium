using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using Scrinium.Core.Documents;
using Scrinium.Core.Sessions;
using Scrinium.Desktop.Services;
using Scrinium.Desktop.ViewModels;
using Scrinium.Ollama;

namespace Scrinium.Tests.Support;

public sealed class DesktopHarness : IDisposable
{
    private static readonly TimeSpan IndexingTimeout = TimeSpan.FromSeconds(30);

    private readonly TempDirectory? _ownedTemp;

    private DesktopHarness(TestWorkspaces app, TempDirectory? ownedTemp)
    {
        App = app;
        _ownedTemp = ownedTemp;
        var monitor = new OllamaMonitor(new OllamaApiClient(new Uri("http://127.0.0.1:1")), app.Settings);
        var factory = new WorkspaceViewModelFactory(app.Factory, app.Settings, monitor, new NoPicker(), NullLoggerFactory.Instance);
        Main = new MainViewModel(app.Catalog, factory, null!, app.Settings, NullLogger<MainViewModel>.Instance);
    }

    public static async Task<DesktopHarness> CreateAsync(FakeChatClient? chat = null)
    {
        var temp = new TempDirectory();
        var harness = new DesktopHarness(new TestWorkspaces(temp.Path, chat), temp);
        await harness.Main.OpenInitialWorkspaceAsync();
        return harness;
    }

    public TestWorkspaces App { get; }

    public MainViewModel Main { get; }

    public WorkspaceViewModel Active => Main.Active ?? throw new InvalidOperationException("No workspace is open.");

    public PanelHost Panel => Active.Panel;

    public SourcesViewModel Sources => Active.Sources;

    public ChatViewModel Chat => Active.Chat;

    public ISessionStore Sessions => Active.Scope.Sessions;

    public async Task<DesktopHarness> ReopenAsync()
    {
        var reopened = new DesktopHarness(App, ownedTemp: null);
        await reopened.Main.OpenInitialWorkspaceAsync();
        return reopened;
    }

    public async Task<Document> AddDocumentAsync(string name, string text)
    {
        var library = Active.Scope.Library;
        var document = await library.ImportAsync(App.WriteFile(name, text));
        var deadline = DateTime.UtcNow + IndexingTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var current = (await library.ListAsync()).Single(d => d.Id == document.Id);
            if (current.State is IndexState.Ready or IndexState.Failed)
            {
                await Sources.InitializeAsync();
                return current;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"{name} was not indexed in time.");
    }

    public async Task AskAsync(string question)
    {
        Chat.Input = question;
        await Chat.SendCommand.ExecuteAsync(null);
    }

    public void Dispose()
    {
        Main.Active?.Sources.Dispose();
        Main.Active?.Scope.StopWorker();
        _ownedTemp?.Dispose();
    }

    private sealed class NoPicker : IFilePicker
    {
        public Task<IReadOnlyList<string>> PickDocumentsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
