using System.Diagnostics;
using System.Text;
using Scrinium.Core.Chat;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Retrieval;

namespace Scrinium.Eval;

public sealed class EvaluationRunner(DocumentLibrary library, IngestionPipeline pipeline, AnswerService answers)
{
    public async Task IndexAsync(IEnumerable<string> files, CancellationToken ct = default)
    {
        foreach (var file in files)
        {
            var document = await library.ImportAsync(file, ct);
            await pipeline.ProcessAsync(document.Id, ct);
        }
    }

    public async Task<CaseResult> RunAsync(EvaluationCase testCase, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var text = new StringBuilder();
        IReadOnlyList<Citation> citations = [];

        await foreach (var answerEvent in answers.AskAsync(testCase.Question, [], ct))
        {
            switch (answerEvent)
            {
                case AnswerEvent.SourcesFound sources:
                    citations = sources.Citations;
                    break;
                case AnswerEvent.TextDelta delta:
                    text.Append(delta.Text);
                    break;
            }
        }

        var answer = text.ToString();
        var cited = CitationMarkers.CitedNumbers(answer);
        var citedTitles = citations
            .Where(c => cited.Contains(c.Number))
            .Select(c => c.DocumentTitle)
            .Distinct()
            .ToList();

        var observation = new AnswerObservation(
            answer,
            citations.Select(c => c.DocumentTitle).Distinct().ToList(),
            citedTitles,
            clock.Elapsed);

        return new CaseResult(testCase, observation, AnswerScorer.Score(testCase, observation));
    }
}
