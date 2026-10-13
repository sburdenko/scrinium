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
        IReadOnlyList<Citation> retrieved = [];

        var history = testCase.History
            .Select(turn => turn.Role == "assistant" ? ChatTurn.Assistant(turn.Text) : ChatTurn.User(turn.Text))
            .ToList();

        await foreach (var answerEvent in answers.AskAsync(testCase.Question, history, ct))
        {
            switch (answerEvent)
            {
                case AnswerEvent.SourcesFound sources:
                    citations = sources.Citations;
                    if (retrieved.Count == 0)
                        retrieved = sources.Citations;
                    break;
                case AnswerEvent.TextDelta delta:
                    text.Append(delta.Text);
                    break;
                case AnswerEvent.SourcesRefined refinedSources:
                    citations = refinedSources.Citations;
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
            retrieved.Select(c => c.DocumentTitle).Distinct().ToList(),
            citedTitles,
            clock.Elapsed,
            citations.Where(c => cited.Contains(c.Number)).Select(c => c.Preview).ToList(),
            retrieved.Select(c => $"[{c.Number}] {c.DocumentTitle}: {c.Preview.ReplaceLineEndings(" ")}").ToList());

        return new CaseResult(testCase, observation, AnswerScorer.Score(testCase, observation));
    }
}
