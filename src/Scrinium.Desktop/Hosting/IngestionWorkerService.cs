using Microsoft.Extensions.Hosting;
using Scrinium.Core.Ingestion;

namespace Scrinium.Desktop.Hosting;

public sealed class IngestionWorkerService(DocumentLibrary library) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        library.RunWorkerAsync(stoppingToken);
}
