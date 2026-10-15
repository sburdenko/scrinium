using System.Text.Json;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Workspaces;

/// <summary>
/// The workspaces under one data folder. Each workspace is a folder of its own with its database and originals,
/// so one workspace cannot read another's documents.
/// </summary>
public sealed class WorkspaceCatalog(string dataRoot)
{
    private const string ManifestName = "workspace.json";
    private const string LegacyDatabase = "scrinium.db";
    private const int MaxNameLength = 60;

    private string WorkspacesRoot => Path.Combine(dataRoot, "workspaces");

    public string DataRoot => dataRoot;

    public IReadOnlyList<Workspace> List()
    {
        if (!Directory.Exists(WorkspacesRoot))
            return [];

        return Directory.EnumerateDirectories(WorkspacesRoot)
            .Select(Path.GetFileName)
            .Select(name => WorkspaceId.TryParse(name, out var id) ? Read(id) : null)
            .OfType<Workspace>()
            .OrderBy(w => w.Id == WorkspaceId.Default ? 0 : 1)
            .ThenBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Workspace? Find(WorkspaceId id) => Directory.Exists(PathsOf(id).Root) ? Read(id) : null;

    public DataPaths PathsOf(WorkspaceId id) => new(Path.Combine(WorkspacesRoot, id.Value));

    /// <summary>Returns the default workspace, moving data from before workspaces into it on first use.</summary>
    public Workspace EnsureDefault(string name)
    {
        MoveLegacyData();
        return Ensure(WorkspaceId.Default, name);
    }

    /// <summary>Returns the workspace with this id, creating it with this name if it does not exist.</summary>
    public Workspace Ensure(WorkspaceId id, string name) => Find(id) ?? Write(id, ValidName(name));

    /// <exception cref="ArgumentException">The name is empty or too long.</exception>
    public Workspace Create(string name)
    {
        var valid = ValidName(name);
        var suggested = WorkspaceId.Suggest(valid);
        var id = suggested;
        for (var number = 2; Directory.Exists(PathsOf(id).Root); number++)
            id = suggested.WithSuffix(number);

        return Write(id, valid);
    }

    private Workspace Write(WorkspaceId id, string name)
    {
        var folder = PathsOf(id).Root;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ManifestName), JsonSerializer.Serialize(new Manifest(name)));
        return new Workspace(id, name);
    }

    private Workspace Read(WorkspaceId id)
    {
        var manifest = Path.Combine(PathsOf(id).Root, ManifestName);
        try
        {
            var name = File.Exists(manifest) ? JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifest))?.Name : null;
            return new Workspace(id, string.IsNullOrWhiteSpace(name) ? id.Value : name);
        }
        catch (JsonException)
        {
            // A damaged manifest only loses the display name; the documents in the folder are still usable.
            return new Workspace(id, id.Value);
        }
    }

    private void MoveLegacyData()
    {
        var legacyDatabase = Path.Combine(dataRoot, LegacyDatabase);
        var target = PathsOf(WorkspaceId.Default);
        if (!File.Exists(legacyDatabase) || Directory.Exists(target.Root))
            return;

        Directory.CreateDirectory(target.Root);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = legacyDatabase + suffix;
            if (File.Exists(file))
                File.Move(file, target.DatabasePath + suffix);
        }

        var originals = Path.Combine(dataRoot, "originals");
        if (Directory.Exists(originals))
            Directory.Move(originals, target.OriginalsDirectory);
    }

    private static string ValidName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A workspace needs a name.", nameof(name));
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"A workspace name is at most {MaxNameLength} characters.", nameof(name));

        return trimmed;
    }

    private sealed record Manifest(string Name);
}
