using Scrinium.Core.Chunking;
using Scrinium.Core.Extraction;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;
using Scrinium.Core.Workspaces;
using Scrinium.Extraction;
using Scrinium.Storage;

namespace Scrinium.Tests.Support;

public sealed class TestWorkspaces
{
    private readonly string _root;

    public TestWorkspaces(string root, FakeChatClient? chat = null, SettingsService? settings = null)
    {
        _root = root;
        Chat = chat ?? new FakeChatClient("ответ");
        Settings = settings ?? new SettingsService(Path.Combine(root, "settings.json"));
        Catalog = new WorkspaceCatalog(Path.Combine(root, "workspace-data"));
        ITextExtractor[] extractors = [new TextFileExtractor(), new PdfTextExtractor()];
        Factory = new WorkspaceScopeFactory(
            Catalog,
            new SqliteWorkspaceStoreFactory(),
            extractors,
            new TextChunker(new ChunkingOptions(MaxTokens: 60, OverlapTokens: 8)),
            Embedder,
            Chat,
            Settings,
            new RetrievalOptions(),
            TimeProvider.System);
    }

    public FakeEmbeddingGenerator Embedder { get; } = new();

    public FakeChatClient Chat { get; }

    public SettingsService Settings { get; }

    public WorkspaceCatalog Catalog { get; }

    public WorkspaceScopeFactory Factory { get; }

    public string WriteFile(string name, string content)
    {
        var path = Path.Combine(_root, "incoming", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
