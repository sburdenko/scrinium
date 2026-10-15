using Scrinium.Core.Retrieval;
using Scrinium.Core.Sessions;
using Scrinium.Core.Workspaces;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class WorkspaceIdTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("kestrel-analytics")]
    [InlineData("a1")]
    public void A_lowercase_slug_is_a_valid_id(string value) =>
        Assert.Equal(value, WorkspaceId.Parse(value).Value);

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../etc")]
    [InlineData("a/b")]
    [InlineData("Upper")]
    [InlineData("-leading")]
    [InlineData("with space")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijx")]
    public void Anything_that_could_leave_the_workspace_folder_is_rejected(string value)
    {
        Assert.False(WorkspaceId.TryParse(value, out _));
        Assert.Throws<FormatException>(() => WorkspaceId.Parse(value));
    }

    [Theory]
    [InlineData("Steuerberatung Müller", "steuerberatung-muller")]
    [InlineData("  Kestrel   Analytics!! ", "kestrel-analytics")]
    [InlineData("Работа", "workspace")]
    public void A_name_suggests_a_readable_id(string name, string expected) =>
        Assert.Equal(expected, WorkspaceId.Suggest(name).Value);
}

public sealed class WorkspaceCatalogTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Root => Path.Combine(_temp.Path, "data");

    [Fact]
    public void An_empty_data_folder_gets_a_default_workspace()
    {
        var catalog = new WorkspaceCatalog(Root);

        var workspace = catalog.EnsureDefault("Personal");

        Assert.Equal(WorkspaceId.Default, workspace.Id);
        Assert.Equal("Personal", workspace.Name);
        Assert.Equal([workspace], catalog.List());
    }

    [Fact]
    public void Each_workspace_has_its_own_folder_and_database()
    {
        var catalog = new WorkspaceCatalog(Root);
        catalog.EnsureDefault("Personal");

        var work = catalog.Create("Work");

        var personal = catalog.PathsOf(WorkspaceId.Default);
        var other = catalog.PathsOf(work.Id);
        Assert.NotEqual(personal.DatabasePath, other.DatabasePath);
        Assert.StartsWith(Path.Combine(Root, "workspaces"), other.Root);
    }

    [Fact]
    public void Creating_a_workspace_with_a_taken_name_gets_a_distinct_id()
    {
        var catalog = new WorkspaceCatalog(Root);

        var first = catalog.Create("Work");
        var second = catalog.Create("Work");

        Assert.Equal("work", first.Id.Value);
        Assert.Equal("work-2", second.Id.Value);
        Assert.Equal(2, catalog.List().Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_workspace_needs_a_name(string name)
    {
        var catalog = new WorkspaceCatalog(Root);

        Assert.Throws<ArgumentException>(() => catalog.Create(name));
    }

    [Fact]
    public void Workspaces_survive_a_new_catalog_on_the_same_folder()
    {
        new WorkspaceCatalog(Root).Create("Kestrel Analytics");

        var listed = Assert.Single(new WorkspaceCatalog(Root).List());

        Assert.Equal("Kestrel Analytics", listed.Name);
        Assert.Equal("kestrel-analytics", listed.Id.Value);
    }

    [Fact]
    public void A_configured_workspace_is_created_once_with_its_id()
    {
        var catalog = new WorkspaceCatalog(Root);

        var first = catalog.Ensure(WorkspaceId.Parse("mueller"), "Steuerberatung Müller");
        var again = catalog.Ensure(WorkspaceId.Parse("mueller"), "Renamed");

        Assert.Equal(first, again);
        Assert.Single(catalog.List());
    }

    [Fact]
    public void Data_from_before_workspaces_moves_into_the_default_workspace()
    {
        Directory.CreateDirectory(Path.Combine(Root, "originals", "2026", "10"));
        File.WriteAllText(Path.Combine(Root, "scrinium.db"), "database");
        File.WriteAllText(Path.Combine(Root, "originals", "2026", "10", "note.md"), "note");
        var catalog = new WorkspaceCatalog(Root);

        catalog.EnsureDefault("Personal");

        var paths = catalog.PathsOf(WorkspaceId.Default);
        Assert.Equal("database", File.ReadAllText(paths.DatabasePath));
        Assert.Equal("note", File.ReadAllText(paths.Resolve(Path.Combine("originals", "2026", "10", "note.md"))));
        Assert.False(File.Exists(Path.Combine(Root, "scrinium.db")));
    }
}

public sealed class WorkspaceScopeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Two_workspaces_never_see_each_others_documents_or_sessions()
    {
        var app = new TestWorkspaces(_temp.Path);
        var personal = app.Catalog.EnsureDefault("Personal");
        var work = app.Catalog.Create("Work");
        await using var first = app.Factory.Open(personal);
        await using var second = app.Factory.Open(work);

        var note = await first.Library.ImportAsync(app.WriteFile("private.txt", "My private note about the engine."));
        await first.Pipeline.ProcessAsync(note.Id);
        await first.Sessions.AppendAsync(Guid.NewGuid(),
            new SavedExchange(DateTimeOffset.UtcNow, "q", "a", ExchangeEnding.Answered, []));

        Assert.Single(await first.Library.ListAsync());
        Assert.Empty(await second.Library.ListAsync());
        Assert.Empty(await second.Sessions.ListRecentAsync(10));
        Assert.IsType<RetrievalOutcome.ArchiveEmpty>(await second.Retriever.RetrieveAsync("engine"));
    }

    [Fact]
    public async Task The_worker_indexes_what_is_imported_while_the_scope_is_open()
    {
        var app = new TestWorkspaces(_temp.Path);
        await using var scope = app.Factory.Open(app.Catalog.EnsureDefault("Personal"));
        scope.StartWorker();

        var document = await scope.Library.ImportAsync(app.WriteFile("note.txt", "Engine noise got louder."));

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var current = (await scope.Library.ListAsync()).Single(d => d.Id == document.Id);
            if (current.State is Scrinium.Core.Documents.IndexState.Ready)
                return;
            await Task.Delay(10);
        }

        Assert.Fail("The worker did not index the document.");
    }
}
