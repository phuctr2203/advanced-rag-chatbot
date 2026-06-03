using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Shared;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<ConfiguredPathResolver>();
builder.Services.AddScoped<UploadedDocumentStorageService>();
builder.Services.AddScoped<TemplateStorageService>();
builder.Services.AddScoped<FormTemplateDetectorService>();
builder.Services.AddScoped<ImageCaptioningService>();
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<DocxParserService>();
builder.Services.AddScoped<XlsxParserService>();
builder.Services.AddScoped<FileConversionService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddSingleton<IVectorStoreService>(serviceProvider => serviceProvider.GetRequiredService<VectorStoreService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var pathResolver = app.Services.GetRequiredService<ConfiguredPathResolver>();
var uploadedDocumentsPath = pathResolver.UploadedDocumentsPath;
Directory.CreateDirectory(uploadedDocumentsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadedDocumentsPath),
    RequestPath = "/documents"
});

var imageStorePath = pathResolver.ImageStorePath;
Directory.CreateDirectory(imageStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});

var templatesStorePath = pathResolver.TemplatesStorePath;
Directory.CreateDirectory(templatesStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(templatesStorePath),
    RequestPath = "/templates"
});

app.MapControllers();

app.Run();
