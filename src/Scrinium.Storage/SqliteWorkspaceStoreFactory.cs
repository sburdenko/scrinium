using Scrinium.Core.Storage;
using Scrinium.Core.Workspaces;

namespace Scrinium.Storage;

public sealed class SqliteWorkspaceStoreFactory : IWorkspaceStoreFactory
{
    public WorkspaceStores Open(DataPaths paths)
    {
        var documents = new SqliteStore(paths.DatabasePath);
        return new WorkspaceStores(documents, documents, new SqliteSessionStore(paths.DatabasePath));
    }
}
