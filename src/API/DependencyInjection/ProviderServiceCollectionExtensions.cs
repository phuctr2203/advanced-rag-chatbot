using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.DependencyInjection;

public static class ProviderServiceCollectionExtensions
{
    public static IServiceCollection AddProviderServices(this IServiceCollection services)
    {
        services.AddHttpClient<ILlmProvider, OpenAICompatibleProvider>();
        services.AddHttpClient<OllamaVisionProvider>();
        services.AddHttpClient<OpenWebUIVisionProvider>();
        services.AddTransient<IVisionProvider, VisionProviderFactory>();
        services.AddHttpClient<IEmbeddingProvider, EmbeddingService>();

        return services;
    }
}
