using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Providers;

public class OpenAICompatibleProvider(HttpClient httpClient, IOptions<LlmProviderOptions> options) : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LlmProviderOptions _options = options.Value;

    public async Task<string> CompleteAsync(string prompt, int maxTokens = 1000, CancellationToken ct = default)
    {
        var endpoint = GetEndpointOptions();
        using var request = CreateRequest(endpoint, prompt, maxTokens, stream: false);
        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken ct)
    {
        var endpoint = GetEndpointOptions();
        using var request = CreateRequest(endpoint, prompt, maxTokens: null, stream: true);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[6..];
            if (data == "[DONE]")
            {
                yield break;
            }

            using var document = JsonDocument.Parse(data);
            var choice = document.RootElement.GetProperty("choices")[0];
            if (choice.GetProperty("delta").TryGetProperty("content", out var content))
            {
                var token = content.GetString();
                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }
        }
    }

    private HttpRequestMessage CreateRequest(LlmEndpointOptions endpoint, string prompt, int? maxTokens, bool stream)
    {
        var uri = new Uri(new Uri(endpoint.BaseUrl.TrimEnd('/') + "/"), "v1/chat/completions");
        var body = new Dictionary<string, object?>
        {
            ["model"] = endpoint.Model,
            ["messages"] = new[] { new { role = "user", content = prompt } },
            ["stream"] = stream
        };

        if (maxTokens is not null)
        {
            body["max_tokens"] = maxTokens.Value;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(endpoint.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
        }

        return request;
    }

    private LlmEndpointOptions GetEndpointOptions()
    {
        var endpoint = _options.Active.Equals("OpenWebUI", StringComparison.OrdinalIgnoreCase)
            ? _options.OpenWebUI
            : _options.Ollama;

        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl) || string.IsNullOrWhiteSpace(endpoint.Model))
        {
            throw new InvalidOperationException("Active LLM provider requires BaseUrl and Model.");
        }

        return endpoint;
    }
}
