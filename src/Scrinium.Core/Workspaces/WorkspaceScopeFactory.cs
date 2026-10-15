using Microsoft.Extensions.AI;
using Scrinium.Core.Chat;
using Scrinium.Core.Chunking;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;

namespace Scrinium.Core.Workspaces;

public sealed class WorkspaceScopeFactory(
    WorkspaceCatalog catalog,
    IWorkspaceStoreFactory stores,
    IEnumerable<ITextExtractor> extractors,
    TextChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IChatClient chat,
    ISettingsProvider settings,
    RetrievalOptions options,
    TimeProvider clock)
{
    private readonly IReadOnlyList<ITextExtractor> _extractors = extractors.ToList();

    public WorkspaceScope Open(Workspace workspace)
    {
        var paths = catalog.PathsOf(workspace.Id);
        var opened = stores.Open(paths);
        var pipeline = new IngestionPipeline(opened.Documents, _extractors, chunker, embedder, settings, paths);
        var library = new DocumentLibrary(opened.Documents, pipeline, paths, clock);
        var retriever = new Retriever(opened.Vectors, opened.Documents, embedder, settings, new PassageLocator(embedder, settings), options);
        var answers = new AnswerService(retriever, new QueryRewriter(chat, settings), new PassageRefiner(embedder, settings), chat, settings);
        var previews = new DocumentPreviewService(opened.Documents, _extractors, paths);
        return new WorkspaceScope(workspace, paths, library, pipeline, retriever, answers, previews, opened.Sessions);
    }
}
