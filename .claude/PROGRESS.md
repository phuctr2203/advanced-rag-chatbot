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
- [x] 1.6 EmbeddingService connected to TEI
- [x] 1.7 VectorStoreService connected to Qdrant

## Phase 2 — Document ingestion pipeline

- [ ] 2.1 PDF parser (text + image extraction)
- [ ] 2.2 PPTX → PDF conversion + pipeline
- [ ] 2.3 DOCX parser (text + image extraction)
- [ ] 2.4 DOC → DOCX conversion + pipeline
- [ ] 2.5 XLSX parser (form field reconstruction)
- [ ] 2.6 Text chunker — Strategy A: fixed-size
- [ ] 2.7 Text chunker — Strategy B: paragraph/semantic boundary
- [ ] 2.8 Text chunker — Strategy C: sliding window with sentence awareness
- [ ] 2.9 Chunker evaluation complete and strategy chosen
- [ ] 2.10 Document classifier — manual selection via API param
- [ ] 2.11 Document classifier — LLM auto-classify fallback
- [ ] 2.12 Full ingestion orchestrator wired end-to-end
- [ ] 2.13 POST /api/ingest endpoint with optional `agent` param
- [ ] 2.14 Static image serving configured

## Phase 3 — RAG query pipeline

- [ ] 3.1 Language detection service
- [ ] 3.2 Intent classifier (hybrid fast-path + LLM)
- [ ] 3.3 Intent response templates for EN, VI, FR, DE
- [ ] 3.4 Vector search across all documents with score filter
- [ ] 3.5 Prompt builder with context and citations
- [ ] 3.6 LLM streaming service
- [ ] 3.7 Source citation parser
- [ ] 3.8 POST /api/chat SSE endpoint
- [ ] 3.9 End-to-end RAG verified in all 4 languages

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
