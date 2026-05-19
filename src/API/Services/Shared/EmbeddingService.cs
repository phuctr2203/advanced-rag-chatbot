using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;
using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Shared;

public class EmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options) : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EmbeddingOptions _options = options.Value;

    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var batchSize = Math.Max(1, _options.BatchSize);
        var embeddings = new List<float[]>(inputs.Count);

        for (var index = 0; index < inputs.Count; index += batchSize)
        {
            var batch = inputs.Skip(index).Take(batchSize).ToArray();
            var uri = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), "embed");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { inputs = batch }, JsonOptions), Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batchEmbeddings = await JsonSerializer.DeserializeAsync<List<float[]>>(stream, JsonOptions, ct)
                ?? throw new InvalidOperationException("Embedding service returned empty response.");

            embeddings.AddRange(batchEmbeddings);
        }

        return embeddings;
    }
}
