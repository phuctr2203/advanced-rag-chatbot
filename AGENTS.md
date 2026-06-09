# ELCA Policy Chatbot — Codex Context

> This file is loaded automatically every session. Always load `PROGRESS.md` alongside it.

---

## Session rules

> - Read `PROGRESS.md` at the start of every session to find the next unchecked task
> - Work only on the current phase — do not jump ahead
> - At the end of every session, verify completed work and update checkboxes in `PROGRESS.md`
> - Do not mark a checkbox unless the feature is verified working end-to-end
> - If a task is partially done, leave it unchecked and add a note in the session notes table
> - Never modify checkboxes in phase files — only update `PROGRESS.md`

---

## Project

| Field | Detail |
|---|---|
| Name | ELCA Policy Chatbot |
| Goal | Chatbot for employees to query company policies in EN, VI, FR, DE |
| Timeline | 12 days |
| Status | See `PROGRESS.md` |

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 / ASP.NET Core |
| Vector DB | Qdrant (Docker, port 6333) |
| Embedding | BAAI/bge-m3 via HuggingFace TEI (Docker, port 8080) |
| LLM — text | gemma4:31b-cloud via Ollama (primary) |
| LLM — vision | gemma4:31b-cloud via Ollama (image captioning at ingestion) |
| LLM — fallback | OpenWebUI (llama33-70b) |
| Frontend | React JS (Vite + TypeScript) |
| MCP | ModelContextProtocol NuGet (Phase 6 only) |

---

## Model config (`appsettings.json`)

```json
{
  "LlmProvider": {
    "Active": "Ollama",
    "OpenWebUI": {
      "BaseUrl": "https://your-company-openwebui.com",
      "ApiKey": "your-key",
      "Model": "llama33-70b"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "ApiKey": "",
      "Model": "gemma4:31b-cloud"
    }
  },
  "VisionProvider": {
    "Active": "Ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "ApiKey": "",
      "Model": "gemma4:31b-cloud"
    }
  },
  "Embedding": {
    "BaseUrl": "http://localhost:8080",
    "BatchSize": 32
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6333,
    "CollectionName": "policy_docs"
  },
  "Ingestion": {
    "ImageStorePath": "../../data/images",
    "TempPath": "../../data/temp",
    "TemplatesStorePath": "../../data/templates",
    "FormRegistryPath": "../../data/form-registry.json",
    "ChunkingStrategy": "ParagraphBoundary",
    "ChunkSizeWords": 400,
    "ChunkOverlapWords": 80,
    "MinimumChunkWords": 30
  }
}
```

> `LlmProvider` and `VisionProvider` use the same model — gemma4:31b-cloud is multimodal. Two separate configs so they can be swapped independently without code changes.

---

## Repository structure

```
policy-bot/
  .Codex/
    AGENTS.md              ← this file (auto-loaded every session)
    PROGRESS.md            ← master checklist (auto-loaded every session)
  docs/
    phases/
      phase-1-infrastructure.md
      phase-2-ingestion.md
      phase-3-rag-pipeline.md
      phase-4-frontend.md
      phase-5-polish.md
      phase-6-agents-mcp.md
    reference/
      data-models.md
      api-contracts.md
      chunking-strategies.md
      intent-classification.md
      llm-providers.md
  docker-compose.yml
  src/
    API/                   ← .NET 10 ASP.NET Core
  Web/                     ← React frontend
  data/
    images/                ← extracted + captioned images (gitignored)
    temp/                  ← temp conversion files (gitignored)
    templates/             ← downloadable DOCX form templates (NOT gitignored)
    form-registry.json     ← manual mapping: form name → aliases → docx file
    form-registry-draft.json ← auto-generated draft from LLM extractor (review before use)
  qdrant_data/             ← Qdrant persistence (gitignored)
  tei_cache/               ← bge-m3 model cache (gitignored)
```

---

## Source code structure (`src/API/`)

```
Controllers/
  ChatController.cs
  IngestController.cs
Services/
  Ingestion/
    DocumentIngestionService.cs
    Parsers/
      PdfParserService.cs
      DocxParserService.cs
      XlsxParserService.cs
      FileConversionService.cs
    TextChunkerService.cs
    DocumentClassifierService.cs
    ImageCaptioningService.cs      ← calls vision provider per image
  Query/
    IntentClassifierService.cs
    LanguageDetectionService.cs
    FormRegistryService.cs             ← loads form-registry.json, lookup by file/alias
    PromptBuilderService.cs
    ChatOrchestrator.cs
  Shared/
    EmbeddingService.cs
    VectorStoreService.cs
Providers/
  ILlmProvider.cs
  IVisionProvider.cs
  OpenAICompatibleProvider.cs
  OllamaVisionProvider.cs
Models/
  ParsedChunk.cs
  ChatRequest.cs
  ChatResponse.cs
  SourceRef.cs
```

---

## Image captioning flow (used in Phase 2)

When a PDF or DOCX page contains images:

1. Extract image bytes from page
2. Skip images smaller than 100×100 px (decorative)
3. Save image to `/data/images/{docName}/page{N}_img{I}.png`
4. Call `IVisionProvider.DescribeImageAsync(imageBytes, surroundingText)` → caption string
5. Create a dedicated **image caption chunk**:
   - `Text` = caption
   - `ChunkType` = `"image_caption"`
   - `ImagePath` = `/images/{docName}/page{N}_img{I}.png`
   - Same `SourceFile`, `PageNumber`, `Agent` as surrounding text chunks
6. Embed caption and upsert to Qdrant alongside text chunks

This makes images **searchable by content**. At retrieval time, if a top result has `chunk_type: "image_caption"`, the frontend shows the image.

---

## Qdrant payload schema

Text chunk:
```json
{
  "source_file": "leave_policy.pdf",
  "page": 3,
  "chunk_index": 1,
  "chunk_type": "text",
  "file_type": "pdf",
  "agent": "ELCA_HR",
  "image_path": ""
}
```

Image caption chunk:
```json
{
  "source_file": "company_overview.pdf",
  "page": 3,
  "chunk_index": 0,
  "chunk_type": "image_caption",
  "file_type": "pdf",
  "agent": "ELCA_GENERAL",
  "image_path": "/images/company_overview/page3_img0.png"
}
```

---

## Coding conventions

- `IAsyncEnumerable<string>` for all streaming operations
- All services registered via DI in `Program.cs`
- Config via `appsettings.json` + `IOptions<T>` — never hardcode URLs or keys
- `CancellationToken` passed through all async methods
- `maxTokens: 10` for all classifier LLM calls (single-word responses)
- Default to `POLICY_QUERY` if intent classifier returns unexpected value
- Default to `ELCA_GENERAL` if document/router classifier returns unexpected value
- Chunk metadata (`source_file`, `page`, `agent`, `chunk_type`, `image_path`) must travel intact through parse → chunk → embed → upsert
- Use `ClosedXML` for Excel — not EPPlus (license concern)

---

## How to load context per session

**Auto-loaded every session:**
- `.Codex/AGENTS.md`
- `.Codex/PROGRESS.md`

**Load for current phase only:**
- `@docs/phases/phase-N-*.md`

**Load reference files as needed:**
- Chunking → `@docs/reference/chunking-strategies.md`
- Query pipeline → `@docs/reference/intent-classification.md`
- LLM/vision calls → `@docs/reference/llm-providers.md`
- Models/schemas → `@docs/reference/data-models.md`
- Endpoints → `@docs/reference/api-contracts.md`

---

## Docker services

```bash
docker compose up -d

curl http://localhost:6333/healthz    # Qdrant — expects healthy JSON
curl http://localhost:8080/health     # TEI — expects OK

docker compose down
```