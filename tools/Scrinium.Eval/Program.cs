using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core;
using Scrinium.Core.Chat;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Retrieval;
using Scrinium.Core.Settings;
using Scrinium.Core.Workspaces;
using Scrinium.Eval;
using Scrinium.Extraction;
using Scrinium.Ollama;
using Scrinium.Storage;

EvalOptions options;
try
{
    options = EvalOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(EvalOptions.Usage);
    return 64;
}

var home = Path.Combine(Path.GetTempPath(), "scrinium-eval-" + Guid.NewGuid().ToString("N"));
try
{
    var services = new ServiceCollection();
    services.AddScriniumCore(Path.Combine(home, "data"), Path.Combine(home, "settings.json"));
    services.AddScriniumSqliteStorage();
    services.AddScriniumTextExtractors();
    services.AddScriniumOllama();
    services.AddSingleton(new TextChunker(options.Chunking));
    services.AddSingleton(new RetrievalOptions(Search: options.Search));
    await using var provider = services.BuildServiceProvider();

    var settings = provider.GetRequiredService<SettingsService>();
    settings.Update(s => s with
    {
        OllamaUrl = options.OllamaUrl ?? s.OllamaUrl,
        ChatModel = options.ChatModel ?? s.ChatModel,
        EmbeddingModel = options.EmbeddingModel ?? s.EmbeddingModel
    });

    var status = await provider.GetRequiredService<OllamaMonitor>().CheckAsync();
    if (status is not OllamaStatus.Ready)
    {
        Console.Error.WriteLine($"Ollama is not ready ({status.GetType().Name}). Start it and pull the models first.");
        return 2;
    }

    var cases = CaseLoader.Load(options.Cases)
        .Where(c => options.Only is null || options.Only.Contains(c.Id))
        .ToList();
    var files = Directory.GetFiles(options.Documents).Where(DocumentKinds.IsSupported).Order().ToList();
    var catalog = provider.GetRequiredService<WorkspaceCatalog>();
    await using var workspace = provider.GetRequiredService<WorkspaceScopeFactory>().Open(catalog.EnsureDefault("Evaluation"));
    var runner = new EvaluationRunner(workspace.Library, workspace.Pipeline, workspace.Answers);

    Console.WriteLine($"Indexing {files.Count} documents with {settings.Current.EmbeddingModel}…");
    await runner.IndexAsync(files);

    var library = workspace.Library;
    var failedDocuments = (await library.ListAsync()).Where(d => d.State is IndexState.Failed).ToList();
    if (failedDocuments.Count > 0)
    {
        foreach (var document in failedDocuments)
            Console.Error.WriteLine($"Could not index {document.Title}: {((IndexState.Failed)document.State).Error}");
        return 3;
    }

    Console.WriteLine($"Running {cases.Count} questions with {settings.Current.ChatModel}…");
    var results = new List<CaseResult>();
    foreach (var testCase in cases)
    {
        var result = await runner.RunAsync(testCase);
        results.Add(result);
        Console.WriteLine($"[{results.Count,2}/{cases.Count}] {(result.Passed ? "pass" : "FAIL")} {testCase.Id} ({result.Observation.Elapsed.TotalSeconds:0.0}s)");
        foreach (var failure in result.Checks.OfType<CheckResult.Failed>())
            Console.WriteLine($"        {failure.Name}: {failure.Reason}");
    }

    var run = new EvaluationRun(
        Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(options.Cases))) ?? "evaluation",
        DateTimeOffset.UtcNow,
        settings.Current.ChatModel,
        settings.Current.EmbeddingModel,
        files.Count,
        results)
    {
        Chunking = options.Chunking,
        Search = options.Search
    };

    var markdown = ReportWriter.Markdown(run);
    if (options.Report is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Report))!);
        await File.WriteAllTextAsync(options.Report, markdown);
        Console.WriteLine($"Report written to {options.Report}");
    }

    var summary = EvaluationSummary.From(results);
    Console.WriteLine($"{summary.Passed} of {summary.Total} cases passed.");

    return options.MinPass is { } minimum && summary.Total > 0 && (double)summary.Passed / summary.Total < minimum ? 1 : 0;
}
finally
{
    if (Directory.Exists(home))
    {
        try { Directory.Delete(home, recursive: true); }
        catch (IOException) { }
    }
}
