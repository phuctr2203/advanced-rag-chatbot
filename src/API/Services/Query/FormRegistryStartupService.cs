using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Query;

public class FormRegistryStartupService(
    FormRegistryService formRegistry,
    ConfiguredPathResolver pathResolver) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        formRegistry.Load(Path.Combine(pathResolver.DataPath, "form-registry.json"));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
