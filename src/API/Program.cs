using PolicyBot.Api.Options;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmProviderOptions>(builder.Configuration.GetSection("LlmProvider"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<ILlmProvider, OpenAICompatibleProvider>();
builder.Services.AddHttpClient<IEmbeddingProvider, EmbeddingService>();
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
