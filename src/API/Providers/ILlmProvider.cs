namespace PolicyBot.Api.Providers;

public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens = 1000, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
