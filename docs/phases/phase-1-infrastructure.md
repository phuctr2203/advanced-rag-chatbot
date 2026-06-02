# Phase 1 — Infrastructure & Project Setup

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: run required infrastructure, scaffold ASP.NET Core API, connect core services, and verify vision provider works before Phase 2 needs it.

---

## Outcomes

- Docker starts Qdrant and HuggingFace TEI
- Qdrant collection `policy_docs` exists with 1024-dimensional cosine vectors
- `src/API` contains ASP.NET Core project structure from `CLAUDE.md`
- Config uses `appsettings.json` and `IOptions<T>`
- LLM provider switchable between OpenWebUI and Ollama without code change
- Vision provider (`IVisionProvider`) implemented and verified — gemma4:31b-cloud describes a test image
- Embedding and vector store services perform a verified round trip

---

## Task 1.1 — Docker environment

Create `docker-compose.yml`:

```yaml
services:
  qdrant:
    image: qdrant/qdrant
    container_name: elca-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - ./qdrant_data:/qdrant/storage
    restart: unless-stopped

  tei:
    image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.5
    container_name: elca-tei
    ports:
      - "8080:80"
    command: --model-id /data/BAAI/bge-m3 --max-client-batch-size 32
    volumes:
      - ./tei_cache:/data
    restart: unless-stopped
```

> Note: `--model-id /data/BAAI/bge-m3` points to the locally downloaded model. Run `hf download BAAI/bge-m3 --local-dir ./tei_cache/BAAI/bge-m3` if the cache is missing.

Verification:
```bash
docker compose up -d
curl http://localhost:6333/healthz
curl http://localhost:8080/health
curl http://localhost:8080/embed -X POST -H "Content-Type: application/json" -d "{\"inputs\": [\"Chính sách nghỉ phép hàng năm là gì?\"]}"
```

Expected:
- Qdrant returns healthy JSON
- TEI returns `OK`
- Embedding response contains one vector with exactly 1024 floats

---

## Task 1.2 — Qdrant collection

```bash
curl -X PUT http://localhost:6333/collections/policy_docs \
  -H "Content-Type: application/json" \
  -d "{\"vectors\": {\"size\": 1024, \"distance\": \"Cosine\"}}"
```

Expected: `{"result":true,"status":"ok"}`

Verify at: `http://localhost:6333/dashboard`

---

## Task 1.3 — API scaffold

Create .NET 10 Web API project in `src/API` with folders:

```
Controllers/
Services/
  Ingestion/
    Parsers/
  Query/
  Shared/
Providers/
Models/
```

NuGet packages to install:

```
Qdrant.Client
UglyToad.PdfPig
DocumentFormat.OpenXml
ClosedXML
NTextCat
Swashbuckle.AspNetCore
```

> Use `ClosedXML` — not EPPlus (license concern).

---

## Task 1.4 — Config and DI

`appsettings.json` sections (see `CLAUDE.md` for full schema):
- `LlmProvider`
- `VisionProvider`
- `Embedding`
- `Qdrant`
- `Ingestion`

Create typed options classes for each section. Register all services in `Program.cs`.

---

## Task 1.5 — LLM provider abstraction

`Providers/ILlmProvider.cs`:

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens = 1000, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

Implement `OpenAICompatibleProvider` targeting `/v1/chat/completions`.

Rules:
- Both OpenWebUI and Ollama are OpenAI-compatible — same implementation, different config
- `maxTokens: 10` for classifier calls
- Stream token deltas for chat responses
- Never hardcode API keys

Register in `Program.cs` based on `LlmProvider:Active` value.

---

## Task 1.6 — Vision provider abstraction

`Providers/IVisionProvider.cs`:

```csharp
public interface IVisionProvider
{
    Task<string> DescribeImageAsync(
        byte[] imageBytes,
        string surroundingText,
        CancellationToken ct = default);
}
```

Implement `OllamaVisionProvider` using Ollama's multimodal API:

```csharp
// POST http://localhost:11434/api/chat
// Body:
{
  "model": "gemma4:31b-cloud",
  "messages": [
    {
      "role": "user",
      "content": "This image is from a company policy document. The surrounding text says: {surroundingText}. Describe what this image shows in 2-3 sentences. Focus on content relevant to company policies.",
      "images": ["{base64ImageBytes}"]
    }
  ],
  "stream": false
}
```

Rules:
- Convert `imageBytes` to base64 before sending
- Return the description string from the response
- If the call fails, return an empty string (do not crash ingestion)
- Register via `IVisionProvider` in DI based on `VisionProvider:Active` config

---

## Task 1.7 — Vision provider verification

Before moving to Phase 2, confirm vision works:

1. Find any image file (`.png` or `.jpg`) on your machine
2. Call `IVisionProvider.DescribeImageAsync` with its bytes
3. Confirm the response is a non-empty descriptive sentence

If gemma4:31b-cloud is not yet pulled in Ollama:
```bash
ollama pull gemma4:31b-cloud
```

Then verify:
```bash
ollama list   # confirm gemma4:31b-cloud appears
```

> This task must pass before starting Phase 2 — image captioning is called in the PDF parser.

---

## Task 1.8 — EmbeddingService

`Services/Shared/EmbeddingService.cs`:

- Accept `IReadOnlyList<string>`
- Batch by `Embedding:BatchSize` (default 32)
- `POST {Embedding:BaseUrl}/embed` with `{ "inputs": [...] }`
- Return `List<float[]>`
- Pass `CancellationToken`

Verification: embed one English and one Vietnamese string, confirm each returns exactly 1024 floats.

---

## Task 1.9 — VectorStoreService

`Services/Shared/VectorStoreService.cs`:

```csharp
public interface IVectorStoreService
{
    Task UpsertAsync(
        IReadOnlyList<ParsedChunk> chunks,
        IReadOnlyList<float[]> vectors,
        CancellationToken ct = default);

    Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        float[] vector,
        string? agent,
        int limit,
        CancellationToken ct = default);
}
```

Rules:
- Connect via `Qdrant.Client` NuGet using `Qdrant:Host` and `Qdrant:Port`
- Upsert payload fields: `source_file`, `page`, `chunk_index`, `chunk_type`, `file_type`, `agent`, `image_path`
- Apply score threshold `0.45f` in `SearchAsync`
- `agent` filter is optional — pass `null` in Phase 3, use it in Phase 6

Verification:
1. Upsert one test chunk with a known embedding
2. Search with the same embedding
3. Confirm returned payload contains all fields correctly

---

## Done criteria

- [ ] Docker health checks pass (Qdrant + TEI)
- [ ] Qdrant collection exists with correct vector config
- [ ] API project builds without errors
- [ ] Config loads all sections correctly
- [ ] LLM provider completes a short test prompt
- [ ] Vision provider returns a description for a test image
- [ ] Embedding service returns 1024-dim vectors for EN and VI text
- [ ] VectorStoreService upsert + search round trip works with correct payload