namespace PolicyBot.Api.Providers;

public class VisionProviderFactory(
    IServiceProvider serviceProvider,
    Microsoft.Extensions.Options.IOptions<Options.VisionProviderOptions> options) : IVisionProvider
{
    public Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, CancellationToken ct = default)
    {
        IVisionProvider provider = options.Value.Active.Equals("OpenWebUI", StringComparison.OrdinalIgnoreCase)
            ? serviceProvider.GetRequiredService<OpenWebUIVisionProvider>()
            : serviceProvider.GetRequiredService<OllamaVisionProvider>();

        return provider.DescribeImageAsync(imageBytes, surroundingText, ct);
    }
}
