using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Value = Qdrant.Client.Grpc.Value;

namespace PolicyBot.Api.Services.Shared;

public class VectorStoreService(IOptions<QdrantOptions> options) : IVectorStoreService
{
    private const float MinimumScore = 0.45f;
    private readonly QdrantOptions _options = options.Value;
    private readonly QdrantClient _client = new(options.Value.Host, options.Value.GrpcPort);

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken: ct);
        if (collections.Any(collection => collection == _options.CollectionName))
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _options.CollectionName,
            new VectorParams { Size = 1024, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task UpsertAsync(IReadOnlyList<ParsedChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken ct = default)
    {
        if (chunks.Count != vectors.Count)
        {
            throw new ArgumentException("Chunks and vectors must have the same count.");
        }

        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureCollectionAsync(ct);

        var points = chunks.Select((chunk, index) => new PointStruct
        {
            Id = (ulong)HashCode.Combine(chunk.SourceFile, chunk.PageNumber, chunk.ChunkIndex),
            Vectors = vectors[index],
            Payload = { ToPayload(chunk) }
        }).ToList();

        await _client.UpsertAsync(_options.CollectionName, points, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, string? agent, int limit, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(agent))
        {
            filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "agent",
                            Match = new Match { Keyword = agent }
                        }
                    }
                }
            };
        }

        var results = await _client.SearchAsync(
            _options.CollectionName,
            vector,
            filter,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: ct);

        return results
            .Where(result => result.Score >= MinimumScore)
            .Select(result => new ScoredChunk
            {
                Score = result.Score,
                Chunk = FromPayload(result.Payload)
            })
            .ToList();
    }

    public async Task<bool> VerifyRoundTripAsync(float[] vector, CancellationToken ct = default)
    {
        var chunk = new ParsedChunk
        {
            Text = "Annual leave policy verification chunk.",
            SourceFile = "phase1_verification.txt",
            PageNumber = 1,
            ChunkIndex = 0,
            ChunkType = "text",
            FileType = "txt",
            Agent = "ELCA_GENERAL",
            ImagePath = string.Empty
        };

        await UpsertAsync([chunk], [vector], ct);
        var results = await SearchAsync(vector, chunk.Agent, 1, ct);
        return results.Any(result =>
            result.Chunk.SourceFile == chunk.SourceFile &&
            result.Chunk.PageNumber == chunk.PageNumber &&
            result.Chunk.ChunkIndex == chunk.ChunkIndex);
    }

    private static Dictionary<string, Value> ToPayload(ParsedChunk chunk)
    {
        return new Dictionary<string, Value>
        {
            ["text"] = chunk.Text,
            ["source_file"] = chunk.SourceFile,
            ["page"] = chunk.PageNumber,
            ["chunk_index"] = chunk.ChunkIndex,
            ["chunk_type"] = chunk.ChunkType,
            ["file_type"] = chunk.FileType,
            ["agent"] = chunk.Agent,
            ["image_path"] = chunk.ImagePath
        };
    }

    private static ParsedChunk FromPayload(IDictionary<string, Value> payload)
    {
        return new ParsedChunk
        {
            Text = GetString(payload, "text"),
            SourceFile = GetString(payload, "source_file"),
            PageNumber = (int)GetInteger(payload, "page"),
            ChunkIndex = (int)GetInteger(payload, "chunk_index"),
            ChunkType = GetString(payload, "chunk_type"),
            FileType = GetString(payload, "file_type"),
            Agent = GetString(payload, "agent"),
            ImagePath = GetString(payload, "image_path")
        };
    }

    private static string GetString(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.StringValue : string.Empty;
    }

    private static long GetInteger(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.IntegerValue : 0;
    }
}
