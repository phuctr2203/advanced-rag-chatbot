namespace PolicyBot.Api.Providers;

public interface IEmbeddingProvider
{
    Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default);
}
