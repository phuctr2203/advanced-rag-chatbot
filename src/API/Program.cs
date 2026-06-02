using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Query;
using PolicyBot.Api.Services.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<LlmProviderOptions>(builder.Configuration.GetSection("LlmProvider"));
builder.Services.Configure<VisionProviderOptions>(builder.Configuration.GetSection("VisionProvider"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<ILlmProvider, OpenAICompatibleProvider>();
builder.Services.AddHttpClient<OllamaVisionProvider>();
builder.Services.AddHttpClient<OpenWebUIVisionProvider>();
builder.Services.AddTransient<IVisionProvider, VisionProviderFactory>();
builder.Services.AddHttpClient<IEmbeddingProvider, EmbeddingService>();
builder.Services.AddScoped<ImageCaptioningService>();
builder.Services.AddScoped<TextChunkerService>();
builder.Services.AddScoped<DocumentClassifierService>();
builder.Services.AddScoped<FormMentionExtractorService>();
builder.Services.AddScoped<FormTemplateDetectorService>();
builder.Services.AddScoped<DocumentIngestionService>();
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<DocxParserService>();
builder.Services.AddScoped<XlsxParserService>();
builder.Services.AddScoped<FileConversionService>();
builder.Services.AddScoped<VectorStoreService>();
builder.Services.AddHttpClient<ProviderStatusService>();
builder.Services.AddScoped<LanguageDetectionService>();
builder.Services.AddScoped<LlmService>();
builder.Services.AddScoped<IntentClassifierService>();
builder.Services.AddScoped<PromptBuilderService>();
builder.Services.AddScoped<SourceCitationParser>();
builder.Services.AddScoped<NoAnswerDetectorService>();
builder.Services.AddScoped<ChatOrchestrator>();
builder.Services.AddSingleton<FormRegistryService>();

var app = builder.Build();

var formRegistryPath = ResolveFormRegistryPath(app.Environment.ContentRootPath);
app.Services.GetRequiredService<FormRegistryService>().Load(formRegistryPath);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var ingestionOptions = app.Services.GetRequiredService<IOptions<IngestionOptions>>().Value;
var imageStorePath = ResolveConfiguredDataPath(app.Environment.ContentRootPath, ingestionOptions.ImageStorePath);
Directory.CreateDirectory(imageStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});

var templatesStorePath = ResolveConfiguredDataPath(app.Environment.ContentRootPath, ingestionOptions.TemplatesStorePath);
Directory.CreateDirectory(templatesStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(templatesStorePath),
    RequestPath = "/templates"
});

app.MapControllers();

app.Run();

static string ResolveFormRegistryPath(string contentRootPath)
{
    var candidates = new[]
    {
        Path.Combine(contentRootPath, "data", "form-registry.json"),
        Path.Combine(contentRootPath, "..", "..", "data", "form-registry.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "data", "form-registry.json")
    };

    return Path.GetFullPath(candidates.FirstOrDefault(File.Exists) ?? candidates[0]);
}

static string ResolveConfiguredDataPath(string contentRootPath, string configuredPath)
{
    if (Path.IsPathRooted(configuredPath))
    {
        return Path.GetFullPath(configuredPath);
    }

    var configuredFullPath = Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    var dataChildName = Path.GetFileName(configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    var repoRootDataPath = Path.GetFullPath(Path.Combine(contentRootPath, "data", dataChildName));

    if (Directory.Exists(Path.Combine(contentRootPath, "data")))
    {
        return repoRootDataPath;
    }

    return configuredFullPath;
}
