using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Providers;

public class OpenWebUIVisionProvider(
    HttpClient httpClient,
    IOptions<VisionProviderOptions> options,
    ILogger<OpenWebUIVisionProvider> logger) : IVisionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly VisionProviderOptions _options = options.Value;

    public async Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, int maxTokens = 300, string mimeType = "image/png", CancellationToken ct = default)
    {
        try
        {
            var endpoint = _options.OpenWebUI;
            var prompt = surroundingText;
            var body = new
            {
                model = endpoint.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}" }
                            }
                        }
                    }
                },
                max_tokens = maxTokens,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(endpoint.BaseUrl.TrimEnd('/') + "/"), "v1/chat/completions"))
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
                .GetProperty("choices")[0]
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
