using Microsoft.Extensions.AI;
using Scrinium.Core.Chat;
using Scrinium.Core.Chunking;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Tests.Support;

public sealed class TestApp : IDisposable
{
    private readonly TempDirectory _temp = new();

    public TestApp(FakeChatClient? chat = null, RetrievalOptions? retrieval = null)
    {
        Embedder = new FakeEmbeddingGenerator();
        Chat = chat ?? new FakeChatClient("ответ");
        Paths = new DataPaths(Path.Combine(_temp.Path, "data"));
        Settings = new SettingsService(Path.Combine(_temp.Path, "settings.json"));
        Store = new SqliteStore(Paths.DatabasePath);

        ITextExtractor[] extractors = [new TextFileExtractor(), new PdfTextExtractor()];
        var chunker = new TextChunker(new ChunkingOptions(MaxTokens: 60, OverlapTokens: 8));
        Pipeline = new IngestionPipeline(Store, extractors, chunker, Embedder, Settings, Paths);
        Library = new DocumentLibrary(Store, Pipeline, Paths, TimeProvider.System);
        Retriever = new Retriever(Store, Store, Embedder, Settings, new PassageLocator(Embedder, Settings), retrieval ?? new RetrievalOptions());
        Answers = new AnswerService(Retriever, Chat, Settings);
        Preview = new DocumentPreviewService(Store, extractors, Paths);
    }

    public FakeEmbeddingGenerator Embedder { get; }
    public FakeChatClient Chat { get; }
    public DataPaths Paths { get; }
    public SettingsService Settings { get; }
    public SqliteStore Store { get; }
    public IngestionPipeline Pipeline { get; }
    public DocumentLibrary Library { get; }
    public Retriever Retriever { get; }
    public AnswerService Answers { get; }
    public DocumentPreviewService Preview { get; }

    public string WriteFile(string name, string content) => _temp.File(name, content);

    public string WriteFile(string name, byte[] content)
    {
        var full = Path.Combine(_temp.Path, name);
        File.WriteAllBytes(full, content);
        return full;
    }

    public async Task<Scrinium.Core.Documents.Document> ImportAndProcessAsync(string path)
    {
        var document = await Library.ImportAsync(path);
        await Pipeline.ProcessAsync(document.Id);
        return (await Store.GetAsync(document.Id))!;
    }

    public void Dispose() => _temp.Dispose();
}
