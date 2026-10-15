using Microsoft.Extensions.Logging;
using Scrinium.Core.Settings;
using Scrinium.Core.Workspaces;
using Scrinium.Desktop.Services;
using Scrinium.Ollama;

namespace Scrinium.Desktop.ViewModels;

public sealed class WorkspaceViewModelFactory(
    WorkspaceScopeFactory scopes,
    SettingsService settings,
    OllamaMonitor monitor,
    IFilePicker picker,
    ILoggerFactory loggers)
{
    public async Task<WorkspaceViewModel> OpenAsync(Workspace workspace)
    {
        var scope = scopes.Open(workspace);
        var logger = loggers.CreateLogger<WorkspaceViewModelFactory>();
        scope.Library.ProcessingFailed += (id, ex) =>
            logger.LogError(ex, "Indexing document {DocumentId} in workspace {Workspace} failed", id, workspace.Id);
        scope.StartWorker();

        var panel = new PanelHost(scope.Previews, settings, scope.Library, monitor, scope.Paths);
        var sources = new SourcesViewModel(scope.Library, settings, picker, panel);
        var chat = new ChatViewModel(scope.Answers, panel, scope.Sessions, loggers.CreateLogger<ChatViewModel>());
        var opened = new WorkspaceViewModel(scope, sources, chat, panel);
        await opened.InitializeAsync();
        return opened;
    }
}
