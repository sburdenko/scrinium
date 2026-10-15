using Scrinium.Core.Sessions;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Workspaces;

public sealed record WorkspaceStores(IDocumentStore Documents, IVectorStore Vectors, ISessionStore Sessions);

/// <summary>Opens the stores of one workspace from its own folder.</summary>
public interface IWorkspaceStoreFactory
{
    WorkspaceStores Open(DataPaths paths);
}
