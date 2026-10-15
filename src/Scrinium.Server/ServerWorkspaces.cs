using Scrinium.Core.Workspaces;

namespace Scrinium.Server;

/// <summary>
/// The workspaces this server answers for. Requests under <c>/api</c> use the default workspace, requests under
/// <c>/w/{workspace}/api</c> the named one; each has its own database, documents and API key.
/// </summary>
public sealed class ServerWorkspaces : IAsyncDisposable
{
    public const string RouteKey = "workspace";

    private readonly Dictionary<WorkspaceId, ServedWorkspace> _served = [];

    public ServerWorkspaces(WorkspaceCatalog catalog, WorkspaceScopeFactory scopes, ServerOptions options)
    {
        Add(scopes, catalog.EnsureDefault("Default"), options.DocumentsFolder, options.ApiKey);

        foreach (var configured in options.Workspaces)
        {
            if (!WorkspaceId.TryParse(configured.Id, out var id))
                throw new InvalidOperationException(
                    $"Workspace id '{configured.Id}' is invalid: use lowercase letters, digits and dashes, at most 40 characters.");
            if (_served.ContainsKey(id))
                throw new InvalidOperationException($"Workspace '{id}' is configured twice.");

            Add(scopes, catalog.Ensure(id, configured.Name ?? id.Value), configured.DocumentsFolder, configured.ApiKey);
        }
    }

    public IEnumerable<ServedWorkspace> All => _served.Values;

    public ServedWorkspace Default => _served[WorkspaceId.Default];

    /// <summary>The workspace named in the route, the default one when the route names none, or null when unknown.</summary>
    public ServedWorkspace? Resolve(HttpContext http)
    {
        var requested = http.GetRouteValue(RouteKey) as string;
        if (requested is null)
            return Default;

        return WorkspaceId.TryParse(requested, out var id) && _served.TryGetValue(id, out var served) ? served : null;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var served in _served.Values)
            await served.Scope.DisposeAsync();
    }

    private void Add(WorkspaceScopeFactory scopes, Workspace workspace, string? documentsFolder, string? apiKey)
    {
        var scope = scopes.Open(workspace);
        scope.StartWorker();
        _served[workspace.Id] = new ServedWorkspace(scope, documentsFolder, apiKey);
    }
}

public sealed record ServedWorkspace(WorkspaceScope Scope, string? DocumentsFolder, string? ApiKey);
