# ELCA Policy Chatbot — Progress

Master checklist mirrors `.claude/IMPLEMENTATION_PLAN.md`. Only check item after feature is verified end-to-end.
> Agent: update checkboxes here at the end of every session. 
> Do not edit phase files.

## Phase 1 — Infrastructure & project setup

- [x] 1.1 Docker environment running (Qdrant + TEI)
- [x] 1.2 Qdrant collection created
- [x] 1.3 ASP.NET Core project scaffolded
- [x] 1.4 DI, config, and folder structure in place
- [x] 1.5 LLM provider abstraction implemented
- [x] 1.6 Vision provider abstraction (`IVisionProvider`, `OllamaVisionProvider`)
- [x] 1.7 Vision provider verified
- [x] 1.8 EmbeddingService connected to TEI
- [x] 1.9 VectorStoreService connected to Qdrant

## Phase 2 — Document ingestion pipeline
 
- [x] 2.1 PDF parser — text extraction per page
- [x] 2.2 `ImageCaptioningService` — multi-layer filter (size, aspect ratio, vision classify, caption)
- [x] 2.3 PDF parser — image extraction using `ImageCaptioningService`
- [x] 2.4 PPTX → PDF conversion via LibreOffice
- [x] 2.5 DOCX parser — text + image extraction using `ImageCaptioningService`
- [x] 2.6 DOC → DOCX conversion via LibreOffice
- [x] 2.7 XLSX parser — form field reconstruction as readable prose
- [x] 2.8 Text chunker — Strategy A: FixedSize
- [x] 2.9 Text chunker — Strategy B: ParagraphBoundary
- [x] 2.10 Text chunker — Strategy C: SentenceWindow
- [x] 2.11 Chunker evaluation — strategy chosen, reason noted below
- [x] 2.12 Document classifier — manual agent via API param
- [x] 2.13 Document classifier — LLM auto-classify fallback
- [x] 2.14 Ingestion orchestrator wired end-to-end
- [x] 2.15 `POST /api/ingest` endpoint working
- [x] 2.16 Static image serving configured (`/images/...`)
- [x] 2.17 `form-registry.json` schema created and manually completed
- [x] 2.18 LLM form mention extractor — draft registry generated from PDF ingestion
- [x] 2.19 DOCX form template detection — `is_form_template` tagged in Qdrant
- [x] 2.20 Static template serving configured (`/templates/...`)

> **Chunker strategy chosen:** ParagraphBoundary — FixedSize, ParagraphBoundary, and SentenceWindow were evaluated against `policy_docs_fixed`, `policy_docs_para`, and `policy_docs_sentence` using three sample documents and five representative questions. All three averaged 4.00/5; ParagraphBoundary was selected on tie because it preserves policy paragraphs and heading-adjacent context for better answer completeness and citation readability. Full report: `docs/evaluations/chunker-evaluation-2026-05-27.md`.
 
## Phase 3 — RAG query pipeline
 
- [x] 3.1 Language detection service
- [x] 3.2 Intent classifier — fast path
- [x] 3.3 Intent classifier — LLM fallback
- [x] 3.4 Intent response templates (all 4 languages)
- [x] 3.5 Vector search (no agent filter)
- [x] 3.6 Prompt builder
- [x] 3.7 LLM streaming service
- [x] 3.8 Source citation parser — updated `SourceRef` with `FormDownload`
- [x] 3.9 `FormRegistryService` — loads `form-registry.json`, lookup by file and alias
- [x] 3.10 Form download enrichment in `ChatOrchestrator`
- [x] 3.11 `POST /api/chat` SSE endpoint — sources include form download refs
- [x] 3.12 End-to-end RAG verified in all 4 languages + form download verified

## Phase 4 — Frontend

- [x] 4.1 React project scaffolded
- [x] 4.2 Chat UI with message history
- [x] 4.3 SSE streaming rendering
- [x] 4.4 Source citation panel
- [x] 4.5 Image display alongside citations
- [x] 4.6 Document upload UI with optional agent selector

## Phase 5 — Polish & demo prep

- [ ] 5.1 Error handling for service and parsing failures
- [ ] 5.2 Multilingual tested end-to-end
- [ ] 5.3 Citation accuracy verified with real policy files
- [ ] 5.4 Demo script prepared
- [ ] 5.5 README written

## Phase 6 — Agent orchestration & MCP

Start only after Phase 5 is complete.

- [ ] 6.1 Router agent
- [ ] 6.2 Vector search updated with agent filter
- [ ] 6.3 Tool-calling loop
- [ ] 6.4 MCP server configured
- [ ] 6.5 MCP tools exposed and externally tested

---
 
## Session notes
 
> Agent appends notes here after each session.
 
| Date | Session summary | Next task |
|---|---|---|
| 2026-05-29 | Completed Phase 4 frontend: created Vite React TypeScript app in `Web/`, implemented Figma-inspired Chat and Documents pages, POST streaming chat via `ReadableStream`, source citations with images and form downloads, document upload with auto/manual agent selection, and Vite proxy to the API. Verified `npm run build`, chat streaming through `http://127.0.0.1:5173/api/chat`, and upload through `http://127.0.0.1:5173/api/ingest`. | 5.1 Error handling for service and parsing failures |
| 2026-05-29 | Completed Phase 3.12 verification: ran annual-leave RAG queries in English, Vietnamese, French, and German with structured `[SOURCES]`; verified payment request form download enrichment and `/templates/Payment_request_form.docx`; verified image-caption citation using CII emergency response flowchart with populated `imagePath` and `/images/...` serving. | 4.1 React project scaffolded |
| 2026-05-28 | Implemented Phase 3 Tasks 3.1-3.11: language detection, intent classifier, static responses, query vector search, prompt builder, LLM streaming wrapper, citation parsing, form registry, form download enrichment, and SSE `/api/chat`. Verified alternate-output API build and smoke-tested `Hi` smalltalk SSE response; full RAG E2E still needs live Qdrant/LLM verification with ingested documents. | 3.12 End-to-end RAG verification |
| 2026-05-27 | Implemented Tasks 2.16-2.20: static `/images` and `/templates` serving, root `data/form-registry.json`, PDF form mention draft extraction, DOCX form template detection, and `is_form_template` Qdrant payload tagging. Verified API builds with `dotnet build --no-restore`; runtime LLM/static-file verification still needs real documents and services. | 3.1 Language detection service |
| 2026-05-27 | Implemented Task 2.14 ingestion orchestrator and Task 2.15 endpoint response/error handling; parse, classify, chunk, embed, and upsert are now wired through `DocumentIngestionService`. Verified API builds with `dotnet build --no-restore`. | 2.16 Static image serving configured (`/images/...`) |
| 2026-05-27 | Implemented Tasks 2.12 and 2.13 with manual `agent` query param validation plus LLM fallback classification from the first 500 words; verified API builds with `dotnet build --no-restore`. | 2.14 Ingestion orchestrator wired end-to-end |
| 2026-05-27 | Completed Task 2.11 chunker evaluation across FixedSize, ParagraphBoundary, and SentenceWindow collections; selected ParagraphBoundary. | 2.12 Document classifier — manual agent via API param |
| 2026-05-27 | Implemented Task 2.10 SentenceWindow chunking and verified API builds with `dotnet build --no-restore`. | 2.11 Chunker evaluation |
| 2026-05-27 | Implemented Task 2.9 ParagraphBoundary chunking and verified API builds with `dotnet build --no-restore`. | 2.10 Text chunker — Strategy C: SentenceWindow |
| | | |
