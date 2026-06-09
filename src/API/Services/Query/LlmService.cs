using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Query;

public class LlmService(ILlmProvider llmProvider)
{
    public IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct = default)
    {
        return llmProvider.StreamAsync(prompt, ct);
    }

    public IAsyncEnumerable<string> StreamAsync(string prompt, float temperature, CancellationToken ct = default)
    {
        return llmProvider.StreamAsync(prompt, temperature, ct);
    }

    public Task<string> CompleteAsync(string prompt, int maxTokens, CancellationToken ct = default)
    {
        return llmProvider.CompleteAsync(prompt, maxTokens, ct);
    }
}
