using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Shared;

public class ProviderStatusService(
    HttpClient httpClient,
    IOptions<LlmProviderOptions> llmOptions,
    IOptions<VisionProviderOptions> visionOptions,
    IOptions<EmbeddingOptions> embeddingOptions,
    IOptions<QdrantOptions> qdrantOptions)
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private readonly LlmProviderOptions _llmOptions = llmOptions.Value;
    private readonly VisionProviderOptions _visionOptions = visionOptions.Value;
    private readonly EmbeddingOptions _embeddingOptions = embeddingOptions.Value;
    private readonly QdrantOptions _qdrantOptions = qdrantOptions.Value;

    public CurrentProviderResponse GetCurrent()
    {
        var llmEndpoint = GetActiveEndpoint(_llmOptions.Active, _llmOptions.OpenWebUI, _llmOptions.Ollama);
        var visionEndpoint = GetActiveEndpoint(_visionOptions.Active, _visionOptions.OpenWebUI, _visionOptions.Ollama);

        return new CurrentProviderResponse
        {
            LlmProvider = _llmOptions.Active,
            LlmModel = llmEndpoint.Model,
            VisionProvider = _visionOptions.Active,
            VisionModel = visionEndpoint.Model,
            EmbeddingBaseUrl = _embeddingOptions.BaseUrl
        };
    }

    public async Task<ProviderStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var llmEndpoint = GetActiveEndpoint(_llmOptions.Active, _llmOptions.OpenWebUI, _llmOptions.Ollama);
        var llmPath = _llmOptions.Active.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "api/tags"
            : "health";

        var llmTask = CheckHttpAsync(_llmOptions.Active, llmEndpoint.BaseUrl, llmPath, ct);
        var embeddingTask = CheckHttpAsync("TEI", _embeddingOptions.BaseUrl, "health", ct);
        var qdrantTask = CheckHttpAsync(
            "Qdrant",
            $"http://{_qdrantOptions.Host}:{_qdrantOptions.Port}",
            "healthz",
            ct);

        await Task.WhenAll(llmTask, embeddingTask, qdrantTask);

        return new ProviderStatusResponse
        {
            Llm = await llmTask,
            Embedding = await embeddingTask,
            Qdrant = await qdrantTask
        };
    }

    private async Task<ServiceStatus> CheckHttpAsync(string name, string baseUrl, string path, CancellationToken ct)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            return new ServiceStatus { Name = name, Message = "Invalid endpoint configuration." };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CheckTimeout);

        try
        {
            using var response = await httpClient.GetAsync(new Uri(baseUri, path), timeoutCts.Token);
            return new ServiceStatus
            {
                Name = name,
                Healthy = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode
                    ? "Reachable"
                    : $"Returned HTTP {(int)response.StatusCode}"
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServiceStatus { Name = name, Message = "Health check timed out." };
        }
        catch (HttpRequestException exception)
        {
            return new ServiceStatus { Name = name, Message = exception.Message };
        }
    }

    private static LlmEndpointOptions GetActiveEndpoint(
        string activeProvider,
        LlmEndpointOptions openWebUi,
        LlmEndpointOptions ollama)
    {
        return activeProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? ollama
            : openWebUi;
    }
}
