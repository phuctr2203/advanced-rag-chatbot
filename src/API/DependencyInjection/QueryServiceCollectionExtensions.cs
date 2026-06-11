using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.DependencyInjection;

public static class QueryServiceCollectionExtensions
{
    public static IServiceCollection AddQueryServices(this IServiceCollection services)
    {
        services.AddSingleton<FormRegistryService>();
        services.AddHostedService<FormRegistryStartupService>();
        services.AddScoped<LanguageDetectionService>();
        services.AddScoped<IntentClassifierService>();
        services.AddScoped<PromptBuilderService>();
        services.AddScoped<LlmService>();
        services.AddScoped<SourceCitationParser>();
        services.AddScoped<FormDownloadEnrichmentService>();
        services.AddScoped<KeywordSearchService>();
        services.AddScoped<HybridSearchService>();
        services.AddScoped<ChatOrchestrator>();
        services.AddScoped<EvaluationChatOrchestrator>();

        return services;
    }
}
