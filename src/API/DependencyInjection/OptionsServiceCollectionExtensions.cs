using PolicyBot.Api.Options;

namespace PolicyBot.Api.DependencyInjection;

public static class OptionsServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));
        services.Configure<VisionProviderOptions>(configuration.GetSection("VisionProvider"));
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embedding"));
        services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
        services.Configure<IngestionOptions>(configuration.GetSection("Ingestion"));

        return services;
    }
}
