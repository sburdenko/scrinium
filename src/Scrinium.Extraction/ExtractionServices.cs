using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Extraction;

namespace Scrinium.Extraction;

public static class ExtractionServices
{
    /// <summary>One reader per supported document kind.</summary>
    public static IReadOnlyList<ITextExtractor> CreateExtractors() =>
        [new TextFileExtractor(), new PdfTextExtractor(), new HtmlTextExtractor(), new DocxTextExtractor()];

    public static IServiceCollection AddScriniumTextExtractors(this IServiceCollection services)
    {
        foreach (var extractor in CreateExtractors())
            services.AddSingleton(extractor);
        return services;
    }
}
