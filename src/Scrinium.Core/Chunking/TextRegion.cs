using Scrinium.Core.Documents;

namespace Scrinium.Core.Chunking;

internal sealed record TextRegion(int Start, int End, ChunkContent Content)
{
    public bool Contains(int position) => position >= Start && position < End;
}
