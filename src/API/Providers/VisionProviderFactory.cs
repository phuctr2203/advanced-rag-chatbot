using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Providers;

public class VisionProviderFactory(OllamaVisionProvider ollamaProvider, IOptions<VisionProviderOptions> options) : IVisionProvider
{
    public Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, CancellationToken ct = default)
    {
        if (!options.Value.Active.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Phase 1 supports Ollama as the active vision provider.");
        }

        return ollamaProvider.DescribeImageAsync(imageBytes, surroundingText, ct);
    }
}
