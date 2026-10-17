using Scrinium.Core.Documents;

namespace Scrinium.Server;

public sealed class DocumentFolderImporter(
    ServerWorkspaces workspaces,
    ILogger<DocumentFolderImporter> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var served in workspaces.All)
            await ImportAsync(served, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task ImportAsync(ServedWorkspace served, CancellationToken ct)
    {
        var folder = served.DocumentsFolder;
        var workspace = served.Scope.Workspace.Id;
        if (string.IsNullOrWhiteSpace(folder))
            return;

        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Documents folder {Folder} of workspace {Workspace} does not exist, nothing to import.", folder, workspace);
            return;
        }

        var library = served.Scope.Library;
        var known = (await library.ListAsync(ct)).Select(d => d.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder).Order())
        {
            if (DocumentKinds.Check(file) is FileSupport.Unsupported unsupported)
            {
                logger.LogInformation("Skipped {File} in workspace {Workspace}: {Reason}", Path.GetFileName(file), workspace, unsupported.Reason);
                continue;
            }

            if (known.Contains(Path.GetFileName(file)))
                continue;

            await library.ImportAsync(file, ct);
            logger.LogInformation("Queued {File} for indexing in workspace {Workspace}.", Path.GetFileName(file), workspace);
        }
    }
}
