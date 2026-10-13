using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Storage;

namespace Scrinium.Storage;

public static class StorageServices
{
    public static IServiceCollection AddScriniumSqliteStorage(this IServiceCollection services)
    {
        services.AddSingleton(sp => new SqliteStore(sp.GetRequiredService<DataPaths>().DatabasePath));
        services.AddSingleton<IDocumentStore>(sp => sp.GetRequiredService<SqliteStore>());
        services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<SqliteStore>());
        return services;
    }
}
