using Microsoft.Extensions.AI;

namespace Scrinium.Tests.Support;

public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int Dimensions = 64;

    public int Calls { get; private set; }

    public int Texts { get; private set; }

    public List<string> Inputs { get; } = [];

    public Exception? FailWith { get; set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        var list = values.ToList();
        Texts += list.Count;
        Inputs.AddRange(list);
        if (FailWith is not null)
            throw FailWith;

        var result = new GeneratedEmbeddings<Embedding<float>>(list.Select(v => new Embedding<float>(Embed(v))).ToList());
        return Task.FromResult(result);
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var words = text.ToLowerInvariant().Split([' ', '\n', '.', ',', '?', '!', ':', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var hash = 17;
            foreach (var c in word)
                hash = unchecked(hash * 31 + c);
            vector[(hash & int.MaxValue) % Dimensions] += 1;
        }

        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
