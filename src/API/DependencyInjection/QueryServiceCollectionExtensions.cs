using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.DependencyInjection;

public static class QueryServiceCollectionExtensions
{
    public static IServiceCollection AddQueryServices(this IServiceCollection services)
    {
        services.AddScoped<LanguageDetectionService>();
        services.AddScoped<IntentClassifierService>();

        return services;
    }
}
