using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Workspaces;

namespace Scrinium.Storage;

public static class StorageServices
{
    public static IServiceCollection AddScriniumSqliteStorage(this IServiceCollection services) =>
        services.AddSingleton<IWorkspaceStoreFactory, SqliteWorkspaceStoreFactory>();
}
