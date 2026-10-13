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
    private const double NumberWeight = 0.3;
    private const int PreviewLength = 220;

    [GeneratedRegex(@"\d+(?:[.,  ]\d{3})*(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    public async Task<IReadOnlyList<Citation>> RefineAsync(
        IReadOnlyList<ContextFragment> fragments,
        string answer,
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

        var texts = work
            .SelectMany(w => w.Spans.Select(span => w.Fragment.ChunkText.Substring(span.Start, span.Length)).Prepend(w.Claim))
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
        var next = 0;
        foreach (var (fragment, spans, claim) in work)
        {
            var claimVector = VectorMath.Normalize(embeddings[next++].Vector.Span);
            var best = (Span: spans[0], Score: double.NegativeInfinity);

            foreach (var span in spans)
            {
                var sentence = fragment.ChunkText.Substring(span.Start, span.Length);
                var vector = VectorMath.Normalize(embeddings[next++].Vector.Span);
                var score = VectorMath.Dot(claimVector, vector) + NumberWeight * NumberOverlap(claim, sentence);
                if (score > best.Score)
                    best = (span, score);
            }

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
        var buffer = "";

        for (var i = 0; i < parts.Count; i++)
        {
            switch (parts[i])
            {
                case AnswerPart.Plain plain:
                    buffer = plain.Text;
                    break;

                case AnswerPart.Marker marker:
                    var claim = LastSentence(buffer);
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

                    buffer = "";
                    break;
            }
        }

        return claims;
    }

    private static string LastSentence(string text) =>
        text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? "";

    private static string FirstSentence(string text) =>
        text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";

    private static double NumberOverlap(string claim, string sentence)
    {
        var wanted = Numbers(claim);
        if (wanted.Count == 0)
            return 0;

        var present = Numbers(sentence);
        return (double)wanted.Count(present.Contains) / wanted.Count;
    }

    private static HashSet<string> Numbers(string text) =>
        NumberPattern().Matches(text)
            .Select(match => new string(match.Value.Where(char.IsDigit).ToArray()))
            .Where(digits => digits.Length > 0)
            .ToHashSet();
}
