using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;

namespace Scrinium.Server;

public sealed class DocumentFolderImporter(
    DocumentLibrary library,
    ServerOptions options,
    ILogger<DocumentFolderImporter> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var folder = options.DocumentsFolder;
        if (string.IsNullOrWhiteSpace(folder))
            return;

        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Documents folder {Folder} does not exist, nothing to import.", folder);
            return;
        }

        var known = (await library.ListAsync(ct)).Select(d => d.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder).Where(DocumentKinds.IsSupported).Order())
        {
            if (known.Contains(Path.GetFileName(file)))
                continue;

            await library.ImportAsync(file, ct);
            logger.LogInformation("Queued {File} for indexing.", Path.GetFileName(file));
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
