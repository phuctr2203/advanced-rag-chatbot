using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Shared;

public class ProviderVisibilityService(
    IOptionsSnapshot<LlmProviderOptions> llmOptions,
    IOptionsSnapshot<VisionProviderOptions> visionOptions,
    IOptionsSnapshot<EmbeddingOptions> embeddingOptions,
    IVectorStoreService vectorStoreService,
    IHttpClientFactory httpClientFactory)
{
    public CurrentProviderResponse GetCurrent()
    {
        var llm = llmOptions.Value;
        var vision = visionOptions.Value;
        var embedding = embeddingOptions.Value;
        var llmEndpoint = GetEndpoint(llm.Active, llm.OpenWebUI, llm.Ollama);
        var visionEndpoint = GetEndpoint(vision.Active, vision.OpenWebUI, vision.Ollama);

        return new CurrentProviderResponse
        {
            LlmProvider = llm.Active,
            LlmModel = llmEndpoint.Model,
            VisionProvider = vision.Active,
            VisionModel = visionEndpoint.Model,
            EmbeddingProvider = "TEI",
            EmbeddingBaseUrl = embedding.BaseUrl
        };
    }

    public async Task<ProviderStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        return new ProviderStatusResponse
        {
            Llm = await CheckLlmAsync(ct),
            Embedding = await CheckEmbeddingAsync(ct),
            Qdrant = await CheckQdrantAsync(ct)
        };
    }

    private async Task<ServiceStatus> CheckLlmAsync(CancellationToken ct)
    {
        var options = llmOptions.Value;
        var endpoint = GetEndpoint(options.Active, options.OpenWebUI, options.Ollama);
        var name = $"{options.Active} LLM";

        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
        {
            return Unhealthy(name, "Base URL is not configured.");
        }

        var relativePath = options.Active.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "api/tags"
            : "api/models";

        return await CheckHttpAsync(name, endpoint.BaseUrl, relativePath, ct);
    }

    private async Task<ServiceStatus> CheckEmbeddingAsync(CancellationToken ct)
    {
        var options = embeddingOptions.Value;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return Unhealthy("TEI embedding", "Base URL is not configured.");
        }

        return await CheckHttpAsync("TEI embedding", options.BaseUrl, "health", ct);
    }

    private async Task<ServiceStatus> CheckQdrantAsync(CancellationToken ct)
    {
        try
        {
            await vectorStoreService.CheckHealthAsync(ct);
            return Healthy("Qdrant", "Reachable.");
        }
        catch (Exception ex)
        {
            return Unhealthy("Qdrant", ex.Message);
        }
    }

    private async Task<ServiceStatus> CheckHttpAsync(string name, string baseUrl, string relativePath, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(BuildUri(baseUrl, relativePath), timeoutCts.Token);
            return response.IsSuccessStatusCode
                ? Healthy(name, "Reachable.")
                : Unhealthy(name, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return Unhealthy(name, ex.Message);
        }
    }

    private static LlmEndpointOptions GetEndpoint(
        string active,
        LlmEndpointOptions openWebUi,
        LlmEndpointOptions ollama)
    {
        return active.Equals("OpenWebUI", StringComparison.OrdinalIgnoreCase) ? openWebUi : ollama;
    }

    private static Uri BuildUri(string baseUrl, string relativePath)
    {
        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), relativePath);
    }

    private static ServiceStatus Healthy(string name, string message)
    {
        return new ServiceStatus { Name = name, Healthy = true, Message = message };
    }

    private static ServiceStatus Unhealthy(string name, string message)
    {
        return new ServiceStatus { Name = name, Healthy = false, Message = message };
    }
}
