# Phase 1 — Infrastructure & Project Setup

Goal: run required infrastructure, scaffold ASP.NET Core API, and connect core services.

## Outcomes

- Docker starts Qdrant and HuggingFace TEI.
- Qdrant collection `policy_docs` exists with 1024-dimensional cosine vectors.
- `src/API` contains ASP.NET Core project structure from `.claude/CLAUDE.md`.
- Config uses `appsettings.json` and `IOptions<T>`.
- LLM provider is switchable between OpenWebUI and Ollama without code change.
- Embedding and vector store services can perform a verified round trip.

## Task 1.1 — Docker environment

Create `docker-compose.yml` with:

- `qdrant/qdrant` on port `6333`
- `ghcr.io/huggingface/text-embeddings-inference:cpu-1.5` on port `8080`
- TEI command `--model-id BAAI/bge-m3`
- volumes:
  - `./qdrant_data:/qdrant/storage`
  - `./tei_cache:/data`

Verification:

```bash
docker compose up -d
curl http://localhost:6333/healthz
curl http://localhost:8080/health
curl http://localhost:8080/embed -X POST -H "Content-Type: application/json" -d "{\"inputs\": [\"Chính sách nghỉ phép hàng năm là gì?\"]}"
```

Expected:

- Qdrant returns healthy JSON.
- TEI returns `OK`.
- Embedding response contains one vector with 1024 floats.

## Task 1.2 — Qdrant collection

Create collection:

```bash
curl -X PUT http://localhost:6333/collections/policy_docs -H "Content-Type: application/json" -d "{\"vectors\": {\"size\": 1024, \"distance\": \"Cosine\"}}"
```

Expected: `{"result":true,"status":"ok"}`.

## Task 1.3 — API scaffold

Create .NET API project in `src/API` and folders:

```text
Controllers/
Services/Ingestion/Parsers/
Services/Query/
Services/Shared/
Providers/
Models/
```

Minimum package set:

- `Qdrant.Client`
- `UglyToad.PdfPig`
- `DocumentFormat.OpenXml`
- `ClosedXML`
- `NTextCat`
- `Swashbuckle.AspNetCore`

Use ClosedXML unless team explicitly approves EPPlus licensing.

## Task 1.4 — Config and DI

`appsettings.json` must include sections:

- `LlmProvider`
- `Embedding`
- `Qdrant`
- `Ingestion`

Create options classes or bind named config sections directly. Register all services in `Program.cs`.

## Task 1.5 — LLM provider abstraction

Create `Providers/ILlmProvider.cs`:

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens = 1000, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

Create `OpenAICompatibleProvider` targeting `/v1/chat/completions`.

Rules:

- Use `maxTokens: 10` for classifier calls.
- Stream token deltas from OpenAI-compatible responses.
- Never hardcode API keys.

## Task 1.6 — EmbeddingService

Implement in `Services/Shared/EmbeddingService.cs`:

- Accept `IReadOnlyList<string>`.
- Batch using `Embedding:BatchSize`, default 32.
- POST to `{Embedding:BaseUrl}/embed` with `{ "inputs": [...] }`.
- Return `List<float[]>`.
- Pass `CancellationToken`.

Verification: embed English and Vietnamese samples, confirm vector count and dimension.

## Task 1.7 — VectorStoreService

Implement in `Services/Shared/VectorStoreService.cs`:

- Ensure collection exists or provide startup method.
- `UpsertAsync(IReadOnlyList<ParsedChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken ct)`.
- `SearchAsync(float[] vector, string? agent, int limit, CancellationToken ct)`.
- Apply score threshold `0.45f`.
- Phase 3 can call without agent filter; Phase 6 uses agent filter.

Verification:

1. Upsert one test chunk.
2. Search with its embedding.
3. Confirm returned payload contains source file, page, chunk index, file type, agent, image paths.

## Done criteria

- Docker health checks pass.
- Collection exists.
- API builds.
- Config loads.
- LLM provider can complete a short prompt.
- Embedding service returns 1024-dimensional vectors.
- Vector store upsert/search round trip works.
