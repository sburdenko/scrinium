using Scrinium.Core.Workspaces;

namespace Scrinium.Desktop.ViewModels;

public sealed class WorkspaceViewModel : IAsyncDisposable
{
    public WorkspaceViewModel(WorkspaceScope scope, SourcesViewModel sources, ChatViewModel chat, PanelHost panel)
    {
        Scope = scope;
        Sources = sources;
        Chat = chat;
        Panel = panel;

        chat.CitationStateChanged += RefreshArchiveMarks;
        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PanelHost.Current))
                RefreshArchiveMarks();
        };
    }

    public WorkspaceScope Scope { get; }

    public Workspace Workspace => Scope.Workspace;

    public SourcesViewModel Sources { get; }

    public ChatViewModel Chat { get; }

    public PanelHost Panel { get; }

    public async Task InitializeAsync()
    {
        await Sources.InitializeAsync();
        await Chat.LoadRecentSessionsAsync();
    }

    public ValueTask DisposeAsync()
    {
        Sources.Dispose();
        return Scope.DisposeAsync();
    }

    private void RefreshArchiveMarks()
    {
        var open = (Panel.Current as PreviewPanelViewModel)?.DocumentId;
        Sources.Mark(Chat.FocusedExchange?.Answer.CitedCitations ?? [], open);
    }
}
