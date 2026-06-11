using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Query;

public class HttpRerankerService(
    HttpClient httpClient,
    IOptions<RerankerOptions> options,
    ILogger<HttpRerankerService> logger) : IRerankerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RerankerOptions _options = options.Value;

    public async Task<RerankResponse> RerankAsync(
        string query,
        IReadOnlyList<ScoredChunk> candidates,
        int limit,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!_options.Enabled)
        {
            return Fallback("disabled", candidates, limit, stopwatch);
        }

        if (candidates.Count == 0)
        {
            return new RerankResponse { Used = false, Results = [] };
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 1)));

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri())
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new RerankerRequest
                    {
                        Model = _options.Model,
                        Query = query,
                        Documents = candidates.Select(candidate => Truncate(candidate.Chunk.Text, _options.MaxDocumentCharacters)).ToList(),
                        TopN = Math.Min(Math.Max(limit, 1), candidates.Count),
                        ReturnDocuments = false
                    }, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var rerankerResponse = JsonSerializer.Deserialize<RerankerHttpResponse>(body, JsonOptions)
                ?? throw new InvalidOperationException("Reranker returned an empty response.");

            var reranked = BuildRerankedResults(candidates, rerankerResponse.Results, limit);
            if (reranked.Count == 0)
            {
                return Fallback("empty_or_malformed_response", candidates, limit, stopwatch);
            }

            stopwatch.Stop();
            return new RerankResponse
            {
                Used = true,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Results = reranked
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Reranker failed; falling back to dense retrieval.");
            if (_options.FallbackToDenseOnError)
            {
                return Fallback(ex.GetType().Name, candidates, limit, stopwatch);
            }

            throw;
        }
    }

    private Uri BuildEndpointUri()
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? string.Empty : "/" + _options.Endpoint.TrimStart('/');
        return new Uri(baseUrl + endpoint);
    }

    private static string Truncate(string text, int maxCharacters)
    {
        if (maxCharacters <= 0 || text.Length <= maxCharacters)
        {
            return text;
        }

        return text[..maxCharacters];
    }

    private static IReadOnlyList<ScoredChunk> BuildRerankedResults(
        IReadOnlyList<ScoredChunk> candidates,
        IReadOnlyList<RerankerResult> results,
        int limit)
    {
        var selected = new List<ScoredChunk>();
        var seen = new HashSet<int>();

        foreach (var result in results.OrderByDescending(result => result.RelevanceScore))
        {
            if (result.Index < 0 || result.Index >= candidates.Count || !seen.Add(result.Index))
            {
                continue;
            }

            selected.Add(new ScoredChunk
            {
                Chunk = candidates[result.Index].Chunk,
                Score = result.RelevanceScore
            });

            if (selected.Count >= limit)
            {
                break;
            }
        }

        if (selected.Count < limit)
        {
            foreach (var fallback in candidates.Select((candidate, index) => new { candidate, index }))
            {
                if (!seen.Add(fallback.index))
                {
                    continue;
                }

                selected.Add(fallback.candidate);
                if (selected.Count >= limit)
                {
                    break;
                }
            }
        }

        return selected;
    }

    private static RerankResponse Fallback(
        string reason,
        IReadOnlyList<ScoredChunk> candidates,
        int limit,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new RerankResponse
        {
            Used = false,
            FallbackReason = reason,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Results = candidates.Take(limit).ToList()
        };
    }

    private class RerankerRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public IReadOnlyList<string> Documents { get; set; } = [];
        public int TopN { get; set; }
        public bool ReturnDocuments { get; set; }
    }

    private class RerankerHttpResponse
    {
        public IReadOnlyList<RerankerResult> Results { get; set; } = [];
    }

    private class RerankerResult
    {
        public int Index { get; set; }
        [JsonPropertyName("relevance_score")]
        public float RelevanceScore { get; set; }
        public float Score { get; set; }
    }
}
