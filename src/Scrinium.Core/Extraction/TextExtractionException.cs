using Scrinium.Core.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Scrinium.Core.Extraction;

public sealed class TextExtractionException(string message) : Exception(message);
