using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Scrinium.Core.Chat;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed class PassageRefiner(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ISettingsProvider settings)
{
    private const double QuestionWeight = 0.3;
    private const double WordWeight = 0.6;
    private const double NeighborCoverage = 0.75;
    private const double SameEvidence = 1e-9;
    private const int PreviewLength = 220;

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
        var asked = question.Trim();
        var questionNumbers = NumberEvidence.Numbers(asked);
        var questionWords = NumberEvidence.Words(asked);
        var answerWords = NumberEvidence.Words(CitationMarkers.Strip(answer));
        var claims = Claims(answer);

        var work = fragments
            .Where(f => claims.ContainsKey(f.Citation.Number) && f.ChunkText.Length > 0)
            .Select(f =>
            {
                var claim = PrimaryClaim(claims[f.Citation.Number], questionNumbers);
                var (target, spans) = ResolveTarget(f, claim, fragments, questionNumbers);
                return (Number: f.Citation.Number, Cited: f, Target: target, Spans: spans, Claim: claim);
            })
            .Where(w => w.Spans.Count > 1 || w.Target != w.Cited)
            .ToList();

        var unchanged = fragments.Select(f => f.Citation).ToList();
        if (work.Count == 0)
            return unchanged;

        var texts = work
            .SelectMany(w => w.Spans.Select(span => Slice(w.Target, span)).Prepend(w.Claim))
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
        var next = 1;
        foreach (var (number, _, target, spans, claim) in work)
        {
            var claimVector = VectorMath.Normalize(embeddings[next++].Vector.Span);
            var scored = new List<(PassageSpan Span, double Numbers, double Likeness)>(spans.Count);

            foreach (var span in spans)
            {
                var sentence = Slice(target, span);
                var vector = VectorMath.Normalize(embeddings[next++].Vector.Span);
                var likeness = VectorMath.Dot(claimVector, vector)
                    + (questionVector is null ? 0 : QuestionWeight * VectorMath.Dot(questionVector, vector))
                    + WordWeight * NumberEvidence.WordOverlap(claim, sentence, questionWords);
                scored.Add((span, NumberEvidence.Overlap(claim, sentence, questionNumbers), likeness));
            }

            var eligible = scored
                .Where(s => s.Numbers > 0 || !SentenceSplitter.IsHeading(target.ChunkText, s.Span))
                .ToList();
            if (eligible.Count == 0)
                continue;

            // A sentence that repeats the figures the answer states is the evidence; likeness only chooses among those.
            var strongest = eligible.Max(s => s.Numbers);
            var best = eligible.Where(s => s.Numbers >= strongest - SameEvidence).MaxBy(s => s.Likeness);
            var expanded = ExpandSpan(target.ChunkText, spans, best.Span, answerWords, questionWords);
            refined[number] = Place(target, expanded, number);
        }

        return fragments.Select(f => refined.GetValueOrDefault(f.Citation.Number) ?? f.Citation).ToList();
    }

    private static string PrimaryClaim(IReadOnlyList<string> claims, IReadOnlySet<string> questionNumbers) =>
        claims.FirstOrDefault(c => NumberEvidence.HasNewNumbers(c, questionNumbers)) ?? claims[0];

    private static (ContextFragment Target, IReadOnlyList<PassageSpan> Spans) ResolveTarget(
        ContextFragment cited,
        string claim,
        IReadOnlyList<ContextFragment> all,
        IReadOnlySet<string> questionNumbers)
    {
        var citedSpans = SentenceSplitter.Split(cited.ChunkText);
        if (!NumberEvidence.HasNewNumbers(claim, questionNumbers)
            || citedSpans.Any(s => NumberEvidence.Overlap(claim, Slice(cited, s), questionNumbers) > 0))
        {
            return (cited, citedSpans);
        }

        var donor = all
            .Where(f => f != cited && f.ChunkText.Length > 0)
            .Select(f =>
            {
                var spans = SentenceSplitter.Split(f.ChunkText);
                var bestNumbers = spans.Count == 0 ? 0 : spans.Max(s => NumberEvidence.Overlap(claim, Slice(f, s), questionNumbers));
                return (Fragment: f, Spans: spans, BestNumbers: bestNumbers);
            })
            .Where(d => d.BestNumbers > 0)
            .OrderByDescending(d => d.BestNumbers)
            .FirstOrDefault();

        return donor.Fragment is not null ? (donor.Fragment, donor.Spans) : (cited, citedSpans);
    }

    private static PassageSpan ExpandSpan(
        string text,
        IReadOnlyList<PassageSpan> spans,
        PassageSpan chosen,
        IReadOnlySet<string> answerWords,
        IReadOnlySet<string> questionWords)
    {
        var index = -1;
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i] == chosen)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return chosen;

        var result = chosen;
        if (index + 1 < spans.Count && ShouldJoin(text, result, spans[index + 1], answerWords, questionWords))
            result = new PassageSpan(result.Start, spans[index + 1].Start + spans[index + 1].Length - result.Start);
        if (index - 1 >= 0 && ShouldJoin(text, spans[index - 1], result, answerWords, questionWords))
            result = new PassageSpan(spans[index - 1].Start, result.Start + result.Length - spans[index - 1].Start);

        return result;
    }

    private static bool ShouldJoin(
        string text,
        PassageSpan left,
        PassageSpan right,
        IReadOnlySet<string> answerWords,
        IReadOnlySet<string> questionWords)
    {
        var combinedLength = right.Start + right.Length - left.Start;
        if (combinedLength > PreviewLength)
            return false;

        var between = text.AsSpan(left.Start + left.Length, right.Start - (left.Start + left.Length));
        if (between.Contains('\n'))
            return false;

        foreach (var candidate in new[] { right, left })
        {
            var words = NumberEvidence.Words(text.Substring(candidate.Start, candidate.Length));
            words.ExceptWith(questionWords);
            if (words.Count < 2 || (double)words.Count(answerWords.Contains) / words.Count < NeighborCoverage)
                return false;
        }

        return true;
    }

    private static string Slice(ContextFragment fragment, PassageSpan span) =>
        fragment.ChunkText.Substring(span.Start, span.Length);

    private static Citation Place(ContextFragment fragment, PassageSpan span, int citationNumber)
    {
        var sentence = Slice(fragment, span);
        var preview = sentence.Length <= PreviewLength ? sentence : sentence[..PreviewLength].TrimEnd() + "…";
        var original = fragment.Citation;

        return original with
        {
            Number = citationNumber,
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
