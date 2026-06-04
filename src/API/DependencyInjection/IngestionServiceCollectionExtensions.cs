using PolicyBot.Api.Services.Ingestion.Chunking;
using PolicyBot.Api.Services.Ingestion.Classification;
using PolicyBot.Api.Services.Ingestion.Forms;
using PolicyBot.Api.Services.Ingestion.Images;
using PolicyBot.Api.Services.Ingestion.Ocr;
using PolicyBot.Api.Services.Ingestion.Orchestration;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.DependencyInjection;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddIngestionServices(this IServiceCollection services)
    {
        services.AddSingleton<ConfiguredPathResolver>();
        services.AddScoped<UploadedDocumentStorageService>();
        services.AddScoped<TemplateStorageService>();
        services.AddScoped<FormTemplateDetectorService>();
        services.AddScoped<FormMentionExtractorService>();
        services.AddScoped<DocumentClassifierService>();
        services.AddScoped<TextChunkerService>();
        services.AddScoped<ImageCaptioningService>();
        services.AddScoped<ImageStorageService>();
        services.AddScoped<PdfTextQualityAnalyzer>();
        services.AddScoped<PdfOcrService>();
        services.AddScoped<PdfImageExtractorService>();
        services.AddScoped<PdfParserService>();
        services.AddScoped<DocxParserService>();
        services.AddScoped<XlsxParserService>();
        services.AddScoped<FileConversionService>();
        services.AddScoped<DocumentIngestionService>();

        return services;
    }
}
