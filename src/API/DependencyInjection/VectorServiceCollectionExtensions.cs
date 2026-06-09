using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.DependencyInjection;

public static class VectorServiceCollectionExtensions
{
    public static IServiceCollection AddVectorServices(this IServiceCollection services)
    {
        services.AddSingleton<VectorStoreService>();
        services.AddSingleton<IVectorStoreService>(serviceProvider => serviceProvider.GetRequiredService<VectorStoreService>());
        services.AddScoped<DocumentDownloadResolver>();
        services.AddScoped<DocumentLibraryService>();

        return services;
    }
}
