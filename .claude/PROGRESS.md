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

- [ ] 2.1 PDF parser — text extraction per page
- [ ] 2.2 PDF parser — image extraction + saved to disk
- [ ] 2.3 PDF parser — image captioning via vision provider
- [ ] 2.4 PPTX → PDF conversion via LibreOffice
- [ ] 2.5 DOCX parser — text + image extraction
- [ ] 2.6 DOC → DOCX conversion via LibreOffice
- [ ] 2.7 XLSX parser — form field reconstruction as readable prose
- [ ] 2.8 Text chunker — Strategy A: FixedSize
- [ ] 2.9 Text chunker — Strategy B: ParagraphBoundary
- [ ] 2.10 Text chunker — Strategy C: SentenceWindow
- [ ] 2.11 Chunker evaluation — strategy chosen, reason noted below
- [ ] 2.12 Document classifier — manual agent via API param
- [ ] 2.13 Document classifier — LLM auto-classify fallback
- [ ] 2.14 Ingestion orchestrator wired end-to-end
- [ ] 2.15 `POST /api/ingest` endpoint working
- [ ] 2.16 Static image serving configured (`/images/...`)

## Phase 3 — RAG query pipeline

- [ ] 3.1 Language detection service
- [ ] 3.2 Intent classifier — fast path
- [ ] 3.3 Intent classifier — LLM fallback
- [ ] 3.4 Intent response templates (all 4 languages)
- [ ] 3.5 Vector search (no agent filter)
- [ ] 3.6 Prompt builder
- [ ] 3.7 LLM streaming service
- [ ] 3.8 Source citation parser
- [ ] 3.9 `POST /api/chat` SSE endpoint
- [ ] 3.10 End-to-end RAG verified in all 4 languages

## Phase 4 — Frontend

- [ ] 4.1 React project scaffolded
- [ ] 4.2 Chat UI with message history
- [ ] 4.3 SSE streaming rendering
- [ ] 4.4 Source citation panel
- [ ] 4.5 Image display alongside citations
- [ ] 4.6 Document upload UI with optional agent selector

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
