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
- [ ] 2.11 Chunker evaluation — strategy chosen, reason noted below
- [x] 2.12 Document classifier — manual agent via API param
- [x] 2.13 Document classifier — LLM auto-classify fallback
- [x] 2.14 Ingestion orchestrator wired end-to-end
- [x] 2.15 `POST /api/ingest` endpoint working
- [x] 2.16 Static image serving configured (`/images/...`)
- [x] 2.17 `form-registry.json` schema created and manually completed
- [x] 2.18 LLM form mention extractor — draft registry generated from PDF ingestion
- [x] 2.19 DOCX form template detection — `is_form_template` tagged in Qdrant
- [x] 2.20 Static template serving configured (`/templates/...`)

### Enhancement Phase 2.1 — parser/storage hardening

- [x] 2.E1 Persistent uploaded document storage under `data/uploads/{yyyyMMdd}/{sha256}_{filename}`
- [x] 2.E2 SHA-256 same-day upload dedupe with `reused` metadata
- [x] 2.E3 Centralized path resolution for uploads, temp, images, and templates
- [x] 2.E4 Static serving for `/documents`, `/images`, and `/templates`
- [x] 2.E5 PDF OCR fallback via OCRmyPDF with text-quality threshold and safe fallback
- [x] 2.E6 Full-page scanned PDF image skip after OCR to avoid duplicate image captions
- [x] 2.E7 Broadened image classification prompt for tools, safety, and facility equipment
- [x] 2.E8 DOC/DOCX form template detection using filename + LLM content classification
- [x] 2.E9 Template storage under `data/templates` with SHA-256 naming and template URL metadata
- [x] 2.E10 Parser responses include document/template metadata for future UI linking
- [x] 2.E11 Optional RecursiveBoundary chunking strategy for difficult OCR or poorly structured text

### Enhancement Phase 2.2 — LLM-assisted form/template mapping suggestions

- [x] 2.2.E1 `data/form-registry-suggestions.json` suggestion file schema
- [x] 2.2.E2 Candidate matching inputs from uploaded template + draft registry entries
- [x] 2.2.E3 LLM mapping prompt returns matched form, confidence, reason, and status
- [x] 2.2.E4 Confidence policy implemented (`recommended`, `needs_manual_review`, unmapped)
- [x] 2.2.E5 Review API for listing, accepting, rejecting, and choosing suggested mappings
- [ ] 2.2.E6 UI review flow shows Accept / Reject / Choose another with confidence score

> **Chunker strategy chosen:** Provisional default is `ParagraphBoundary`.
> 
> Reason: company policy documents are usually structured by articles, sections, clauses, and paragraphs, so paragraph-aware chunks preserve policy context and citation precision better than fixed word windows. Keep `RecursiveBoundary` available as the fallback for OCR-heavy or poorly structured documents. Final confirmation will be done later with retriever evaluation using context precision, context recall, context relevance, answer completeness, and citation precision.
 
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

### Enhancement Phase 3.1 — Hybrid retrieval

- [x] 3.H1 Hybrid retrieval configuration
- [x] 3.H2 Keyword search service for exact terms, codes, form names, and acronyms
- [x] 3.H3 Hybrid search service with score merge/rerank
- [x] 3.H4 Chat pipeline integration with hybrid retrieval
- [x] 3.H5 bge-m3 sparse-vector decision documented
- [x] 3.H6 Hybrid retrieval verification and dense/keyword/hybrid comparison

## Phase 4 — Frontend

- [ ] 4.1 React project scaffolded
- [ ] 4.2 Chat UI with message history
- [ ] 4.3 SSE streaming rendering
- [ ] 4.4 Source citation panel
- [ ] 4.5 Image display alongside citations
- [ ] 4.6 Document upload UI with optional agent selector
- [ ] 4.7 Form mapping suggestion review UI with Accept / Reject / Choose another

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

## RAGAS Evaluation

- [ ] EVAL 1 Corpus audit — inspect all 44 files, extract parser output, classify canonical and supporting sources
- [ ] EVAL 2 Question draft generation — create approximately 100 grounded draft questions
- [ ] EVAL 3 Dataset validation — review references, sources, pages, language, intent, and duplicates
- [ ] EVAL 4 Evaluation query endpoint — expose trace metadata behind `Evaluation:Enabled`
- [ ] EVAL 5 Offline RAGAS runner — run both evaluator profiles with resumable output
- [ ] EVAL 6 DOCX report generation — export quantitative and deterministic checks
- [ ] EVAL 7 Baseline and tuning loop — preserve baseline and compare improvements

---
 
## Session notes
 
> Agent appends notes here after each session.

- 2026-06-04: Verified task 2.15 against running API on `http://localhost:5000`: invalid agent returns `400`, unsupported `.md` upload returns `400`, and DOCX ingest with `agent=ELCA_GENERAL` returns `200` with `chunks: 60`, document metadata, and template metadata.
- 2026-06-04: Implemented task 2.18 form mention extractor and wired it into PDF ingestion before chunking. User verified PDF ingestion generated `data/form-registry-draft.json`.
- 2026-06-04: Verified task 2.19 by querying Qdrant collection `rag_policy_docs`; DOCX template chunks include `is_form_template: true` and `template_path`.
- 2026-06-04: Verified task 2.20 against running API on `http://localhost:5000`; `/templates/9ee96f17670f8f0e_Payment%20request%20form.docx` returned `200` with DOCX content type.
- 2026-06-04: Implemented Enhancement Phase 2.2 backend. Verified template upload generated `data/form-registry-suggestions.json` using draft entries + template metadata; local LLM was unavailable, so filename/template-text heuristic fallback produced a `pending_review` suggestion with confidence `0.89`. Verified review endpoints for list, choose-template, accept, and reject. UI review flow remains unchecked.
- 2026-06-05: Implemented Phase 3 tasks 3.1-3.4 query foundation. Verified language detection and fast-path intent via `POST /verify/query-intent` for English, Vietnamese, French, and German deterministic cases. Verified static intent responses for smalltalk/no-results. Task 3.3 LLM fallback is implemented with safe default to `POLICY_QUERY`, but left unchecked because the local verification run could not reach the configured LLM endpoint.
- 2026-06-05: Verified task 3.3 after API restart on `http://localhost:5000`; `POST /verify/query-intent` with `What is the capital of France?` returned `intent: OutOfScope` using LLM language detection/fallback and the out-of-scope response template.
- 2026-06-05: Implemented and verified task 3.5 vector search. `POST /verify/vector-search` embeds the query, searches Qdrant with `agent = null`, applies the `0.45` score threshold, and returns top results with text/image-caption metadata. Verified locally with `fire hose cabinet and fire alarm equipment in CII Tower`; returned 6 results including `image_caption` and `text` chunks.
- 2026-06-05: Implemented and verified tasks 3.6-3.8. `POST /verify/prompt-builder` verified grounded context prompt + required `SOURCES` format, `POST /verify/llm-service` verified completion and streaming through the configured LLM provider, and `POST /verify/source-citations` verified citation parsing with image-caption `ImagePath` plus template `FormDownload`.
- 2026-06-05: Implemented and verified tasks 3.9-3.10. `FormRegistryService` loads `data/form-registry.json` at startup, tolerates missing/malformed files, reloads when the registry file changes, and supports lookup by template file or alias. `ChatOrchestrator` now enriches final source events with template download refs. Verified with `POST /verify/form-registry` and `POST /verify/form-download-enrichment`.
- 2026-06-05: Implemented and verified task 3.11. `POST /api/chat` streams SSE `data:` events and finishes with `[SOURCES]` JSON. Verified smalltalk, out-of-scope, no-results, and CII Tower RAG questions in EN/VI/FR/DE against a Development API using the configured LLM provider. Updated citation parsing to handle LLM citations like `page 7, 8, 9`.
- 2026-06-05: Initial task 3.12 verification was blocked because the available Qdrant data only contained CII Tower content; the annual-leave corpus, final registry mapping, and downloadable payment-request template were not available yet.
- 2026-06-05: Verified task 3.12 after ingesting annual-leave and payment-request documents/templates. Annual-leave questions in EN/VI/FR/DE returned grounded SSE answers with `Attribution of additional annual leave days with seniority.pdf` citations. Payment-request queries returned `/templates/9ee96f17670f8f0e_Payment request form.docx` in both alias-level `formDownloads` and direct DOCX source `formDownload`. Static template download returned `200 OK`. CII Tower image query returned `image_caption` sources with populated `imagePath`. Smalltalk, out-of-scope, and no-results paths returned empty source events as expected.
- 2026-06-05: Added Enhancement Phase 3.1 hybrid retrieval plan to `docs/phases/phase-3-rag-pipeline.md` and mirrored unchecked tasks in this progress file. Scope covers retrieval config, keyword search, hybrid reranking, chat integration, bge-m3 sparse-vector investigation, and verification comparing dense/keyword/hybrid results.
- 2026-06-05: Implemented and verified Enhancement Phase 3.1 tasks 3.H1-3.H5. Added hybrid search options, keyword search over Qdrant chunk payloads, hybrid dense/keyword merge and rerank, and direct chat integration through `HybridSearchService`. Documented bge-m3 sparse-vector support as a future optimization because current TEI `/embed` integration returns dense vectors only. Verified hybrid retrieval for `SCH-HR-003`, `Payment Request Form`, `GIẤY ĐỀ NGHỊ THANH TOÁN`, `Article 4 annual leave`, and `PCCC equipment`; chat-level `SCH-HR-003` returned annual-leave sources. `3.H6` remains open for formal dense/keyword/hybrid comparison reporting.
- 2026-06-05: Verified Enhancement Phase 3.1 task 3.H6 against 177 Qdrant points. Dense/keyword/hybrid comparison covered semantic annual leave, `SCH-HR-003`, `Payment Request Form`, `GIẤY ĐỀ NGHỊ THANH TOÁN`, `PCCC equipment`, and `Article 4 annual leave`. Added `docs/reports/hybrid-retrieval-comparison.md`. Result: keep `Hybrid` as the default retrieval mode because it preserves semantic recall and recovers exact-code/form/acronym queries that dense search can miss.
- 2026-06-05: Simplified hybrid retrieval enhancement after choosing hybrid as final behavior. Removed runtime retrieval mode switching and debug-only helper endpoints/classes. Renamed retrieval config to `HybridSearch`; `ChatOrchestrator` now injects `HybridSearchService` directly. Kept dense/keyword outputs only in `/verify/hybrid-search` diagnostics for reporting. Verified `SCH-HR-003` through diagnostics and `/api/chat`.
 
