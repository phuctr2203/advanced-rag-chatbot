using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Providers;

public class OllamaVisionProvider(
    HttpClient httpClient,
    IOptions<VisionProviderOptions> options,
    ILogger<OllamaVisionProvider> logger) : IVisionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly VisionProviderOptions _options = options.Value;

    public async Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, CancellationToken ct = default)
    {
        try
        {
            var endpoint = _options.Ollama;
            var prompt = $"This image is from a company policy document. The surrounding text says: {surroundingText}. Describe what this image shows in 2-3 sentences. Focus on content relevant to company policies.";
            var body = new
            {
                model = endpoint.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt,
                        images = new[] { Convert.ToBase64String(imageBytes) }
                    }
                },
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(endpoint.BaseUrl.TrimEnd('/') + "/"), "api/chat"))
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(endpoint.ApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
            }

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vision provider returned {StatusCode}.", response.StatusCode);
                return string.Empty;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Vision provider failed.");
            return string.Empty;
        }
    }
}
