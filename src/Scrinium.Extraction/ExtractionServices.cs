using Microsoft.Extensions.DependencyInjection;
using Scrinium.Core.Extraction;

namespace Scrinium.Extraction;

public static class ExtractionServices
{
    public static IServiceCollection AddScriniumTextExtractors(this IServiceCollection services)
    {
        services.AddSingleton<ITextExtractor, TextFileExtractor>();
        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        return services;
    }
}
