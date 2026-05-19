# ELCA Policy Chatbot — Claude Code Context

> This file is loaded automatically every session. Keep it open alongside `docs/PROGRESS.md` and the current phase file.

## Session rules

- Read `PROGRESS.md` at the start of every session to see what's done
- Work only on the current phase — do not jump ahead
- At the end of every session, verify completed work and update 
  checkboxes in `PROGRESS.md`
- Do not mark a checkbox unless the feature is verified working end-to-end
- If a task is partially done, leave it unchecked and add a note below it

---

## Project

| Field | Detail |
|---|---|
| Name | ELCA Policy Chatbot |
| Goal | Chatbot for employees to query company policies in EN, VI, FR, DE |
| Timeline | 12 days |
| Status | See `docs/PROGRESS.md` |

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 / ASP.NET Core |
| Vector DB | Qdrant (Docker, port 6333) |
| Embedding | BAAI/bge-m3 via HuggingFace TEI (Docker, port 8080) |
| LLM | Llama 3.3 70B — company OpenWebUI (switchable to Ollama) |
| Frontend | React JS (Vite + TypeScript) |
| MCP | ModelContextProtocol NuGet (Phase 6 only) |

---

## Repository structure

```
policy-bot/
  .claude/
    CLAUDE.md                  ← this file (always loaded)
  docs/
    PROGRESS.md                ← master checklist — always load this
    phases/
      phase-1-infrastructure.md
      phase-2-ingestion.md
      phase-3-rag-pipeline.md
      phase-4-frontend.md
      phase-5-polish.md
      phase-6-agents-mcp.md
    reference/
      architecture.md          ← service structure, data flow
      data-models.md           ← ParsedChunk, ChatRequest, Qdrant payload
      api-contracts.md         ← all endpoint specs
      chunking-strategies.md   ← 3 chunking approaches + evaluation guide
      intent-classification.md ← hybrid classifier, prompts, response templates
      llm-providers.md         ← ILlmProvider, OpenWebUI vs Ollama config
  docker-compose.yml
  src/
    API/                       ← .NET 10 ASP.NET Core project
  Web/                         ← React frontend
  data/
    images/                    ← extracted document images (gitignored)
    temp/                      ← temp files during ingestion (gitignored)
  qdrant_data/                 ← Qdrant persistence (gitignored)
  tei_cache/                   ← bge-m3 model cache (gitignored)
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
  Query/
    IntentClassifierService.cs
    LanguageDetectionService.cs
    PromptBuilderService.cs
    ChatOrchestrator.cs
  Shared/
    EmbeddingService.cs
    VectorStoreService.cs
Providers/
  ILlmProvider.cs
  OpenAICompatibleProvider.cs
Models/
  ParsedChunk.cs
  ChatRequest.cs
  ChatResponse.cs
  SourceRef.cs
```

---

## Coding conventions

- Use `IAsyncEnumerable<string>` for all streaming operations
- Register all services via DI in `Program.cs`
- Config in `appsettings.json` + `IOptions<T>` — never hardcode URLs or keys
- Pass `CancellationToken` through all async methods
- `maxTokens: 10` for all classifier LLM calls (single-word responses)
- Default to `POLICY_QUERY` if intent classifier returns unexpected value
- Default to `ELCA_GENERAL` if document/router classifier returns unexpected value
- Chunk metadata (source file, page, agent, image paths) must travel with the chunk through the entire pipeline

---

## How to load context per session

**Always load:**
- `.claude/CLAUDE.md` (auto-loaded)
- `docs/PROGRESS.md`

**Load for current phase only:**
- `docs/phases/phase-N-*.md`

**Load reference files as needed:**
- Working on chunking → `docs/reference/chunking-strategies.md`
- Working on query pipeline → `docs/reference/intent-classification.md`
- Working on LLM calls → `docs/reference/llm-providers.md`
- Checking models/schemas → `docs/reference/data-models.md`
- Checking endpoints → `docs/reference/api-contracts.md`

---

## Docker services

```bash
# Start all services
docker compose up -d

# Health checks
curl http://localhost:6333/healthz   # Qdrant
curl http://localhost:8080/health    # TEI

# Stop
docker compose down
```

