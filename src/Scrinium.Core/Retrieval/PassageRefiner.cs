using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Scrinium.Core.Chat;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed partial class PassageRefiner(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings)
{
    private const double QuestionWeight = 0.3;
    private const double SameEvidence = 1e-9;
    private const int PreviewLength = 220;

    [GeneratedRegex(@"\d+(?:[.,  ]\d{3})*(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// Moves each cited fragment's highlight to the sentence that best supports what the answer claims for it,
    /// leaning towards sentences about what was asked.
    /// </summary>
    public async Task<IReadOnlyList<Citation>> RefineAsync(
        IReadOnlyList<ContextFragment> fragments,
        string answer,
        string question,
        CancellationToken ct = default)
    {
        var claims = Claims(answer);
        var work = fragments
            .Where(f => claims.ContainsKey(f.Citation.Number) && f.ChunkText.Length > 0)
            .Select(f => (Fragment: f, Spans: SentenceSplitter.Split(f.ChunkText), Claim: string.Join(" ", claims[f.Citation.Number])))
            .Where(w => w.Spans.Count > 1)
            .ToList();

        var unchanged = fragments.Select(f => f.Citation).ToList();
        if (work.Count == 0)
            return unchanged;

        var asked = question.Trim();
        var texts = work
            .SelectMany(w => w.Spans.Select(span => w.Fragment.ChunkText.Substring(span.Start, span.Length)).Prepend(w.Claim))
            .Prepend(asked.Length > 0 ? asked : " ")
            .ToList();

        GeneratedEmbeddings<Embedding<float>> embeddings;
        try
        {
            var options = new EmbeddingGenerationOptions { ModelId = settings.Current.EmbeddingModel };
            embeddings = await embedder.GenerateAsync(texts, options, ct);
        }
        catch (HttpRequestException)
        {
            // Pointing at the right sentence is a refinement; keep the first guess when the model server drops out.
            return unchanged;
        }

        var refined = new Dictionary<int, Citation>();
        var questionVector = asked.Length > 0 ? VectorMath.Normalize(embeddings[0].Vector.Span) : null;
        var questionNumbers = NumberEvidence.Numbers(asked);
        var next = 1;
        foreach (var (fragment, spans, claim) in work)
        {
            var claimVector = VectorMath.Normalize(embeddings[next++].Vector.Span);
            var scored = new List<(PassageSpan Span, double Numbers, double Likeness)>(spans.Count);

            foreach (var span in spans)
            {
                var sentence = fragment.ChunkText.Substring(span.Start, span.Length);
                var vector = VectorMath.Normalize(embeddings[next++].Vector.Span);
                var likeness = VectorMath.Dot(claimVector, vector)
                    + (questionVector is null ? 0 : QuestionWeight * VectorMath.Dot(questionVector, vector));
                scored.Add((span, NumberEvidence.Overlap(claim, sentence, questionNumbers), likeness));
            }

            // A sentence that repeats the figures the answer states is the evidence; likeness only chooses among those.
            var strongest = scored.Max(s => s.Numbers);
            var best = scored.Where(s => s.Numbers >= strongest - SameEvidence).MaxBy(s => s.Likeness);
            refined[fragment.Citation.Number] = Place(fragment, best.Span);
        }

        return fragments.Select(f => refined.GetValueOrDefault(f.Citation.Number) ?? f.Citation).ToList();
    }

    private static Citation Place(ContextFragment fragment, PassageSpan span)
    {
        var sentence = fragment.ChunkText.Substring(span.Start, span.Length);
        var preview = sentence.Length <= PreviewLength ? sentence : sentence[..PreviewLength].TrimEnd() + "…";
        var original = fragment.Citation;

        return original with
        {
            Preview = preview,
            Location = new SourceLocation(original.DocumentId, original.Page, fragment.ChunkOffset + span.Start, span.Length)
        };
    }

    private static Dictionary<int, List<string>> Claims(string answer)
    {
        var claims = new Dictionary<int, List<string>>();
        var parts = CitationMarkers.Split(answer);
        var sinceLastMarker = new StringBuilder();

        for (var i = 0; i < parts.Count; i++)
        {
            switch (parts[i])
            {
                case AnswerPart.Plain plain:
                    sinceLastMarker.Append(plain.Text);
                    break;

                case AnswerPart.Marker marker:
                    var claim = Clean(sinceLastMarker.ToString());
                    if (claim.Length == 0 && i + 1 < parts.Count && parts[i + 1] is AnswerPart.Plain following)
                        claim = FirstSentence(following.Text);

                    if (claim.Length > 0)
                    {
                        foreach (var number in marker.Numbers)
                        {
                            if (!claims.TryGetValue(number, out var list))
                                claims[number] = list = [];
                            list.Add(claim);
                        }
                    }

                    sinceLastMarker.Clear();
                    break;
            }
        }

        return claims;
    }

    private static string Clean(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim('.', ',', ';', ':', ' ');

    private static string FirstSentence(string text)
    {
        var cleaned = Clean(text);
        var spans = SentenceSplitter.Split(cleaned);
        return spans.Count == 0 ? "" : cleaned.Substring(spans[0].Start, spans[0].Length);
    }

}
