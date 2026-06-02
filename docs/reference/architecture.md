# Architecture Reference

## System overview

ELCA Policy Chatbot uses Retrieval-Augmented Generation.

```text
User → React UI → ASP.NET Core API → Intent/Language services
                                 → Embedding service → TEI bge-m3
                                 → VectorStore service → Qdrant
                                 → Prompt builder → LLM provider
                                 → SSE stream → React UI
```

## Backend structure

```text
src/API/
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
      RouterAgentService.cs
      LanguageDetectionService.cs
      PromptBuilderService.cs
      ChatOrchestrator.cs
    Shared/
      EmbeddingService.cs
      VectorStoreService.cs
  Providers/
    ILlmProvider.cs
    IEmbeddingProvider.cs
    OpenAICompatibleProvider.cs
  Models/
```

## Ingestion data flow

```text
Upload file
  → validate extension
  → convert DOC/PPTX when needed
  → parse text and images
  → classify agent manually or via LLM
  → chunk text with metadata
  → embed chunks
  → upsert vectors and payloads to Qdrant
```

## Query data flow

Phase 3:

```text
Chat request
  → language detection
  → intent classifier
  → static response if smalltalk/out-of-scope
  → embed query
  → Qdrant search across all docs
  → build RAG prompt
  → stream LLM answer
  → parse sources
  → final SSE sources event
```

Phase 6 adds router:

```text
Policy query
  → router agent chooses ELCA_HR / ELCA_GENERAL / CII_TOWER_SUPPORT
  → Qdrant search with agent payload filter
```

## External services

| Service | Port | Purpose |
|---|---:|---|
| Qdrant | 6333 | Vector database |
| TEI | 8080 | bge-m3 embeddings |
| OpenWebUI/Ollama | configurable | OpenAI-compatible LLM API |

## Configuration principles

- All URLs and keys from `appsettings.json`.
- No hardcoded API keys.
- Provider switching through config only.
- `CancellationToken` flows from controller to services.
- Classifier calls use `maxTokens: 10`.
