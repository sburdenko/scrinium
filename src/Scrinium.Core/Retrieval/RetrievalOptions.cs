using Microsoft.Extensions.AI;
using Scrinium.Core.Chunking;
using Scrinium.Core.Documents;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Retrieval;

public sealed record RetrievalOptions(
    int TopK = 6,
    int ContextTokenBudget = 3000,
    int PreviewLength = 220,
    double MinScore = 0.35,
    double ScoreMargin = 0.10);
