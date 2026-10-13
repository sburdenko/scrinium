using Microsoft.Extensions.Hosting;

namespace Scrinium.Core.Ingestion;

public sealed class IngestionWorkerService(DocumentLibrary library) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        library.RunWorkerAsync(stoppingToken);
}
