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
builder.Services.AddScoped<ImageCaptioningService>();
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<DocxParserService>();
builder.Services.AddScoped<XlsxParserService>();
builder.Services.AddScoped<FileConversionService>();
builder.Services.AddSingleton<VectorStoreService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
