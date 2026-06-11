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
- [x] 2.2.E6 UI review flow shows Accept / Reject / Choose another with confidence score

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

- [x] 4.1 React project scaffolded
- [x] 4.2 Chat UI with message history
- [x] 4.3 SSE streaming rendering
- [x] 4.4 Source citation panel
- [x] 4.5 Image display alongside citations
- [x] 4.6 Document upload UI with optional agent selector
- [x] 4.7 Form mapping suggestion review UI with Accept / Reject / Choose another

## Enhancement Phase 1 — Retrieval UX, language robustness, and system visibility

- [x] E1.3 Documents library — backend Qdrant document listing + frontend library view

## Phase 5 — Polish & demo prep

- [ ] 5.1 Error handling for service and parsing failures
- [ ] 5.2 Multilingual tested end-to-end
- [ ] 5.3 Citation accuracy verified with real policy files
- [ ] 5.4 Demo script prepared
- [ ] 5.5 README written

## Evaluation Phase — RAGAS answer and retrieval evaluation

- [x] EV.1 RAGAS evaluation plan documented in `evaluation/ragas-evaluation-plan.md`
- [x] EV.2 Non-streaming `POST /api/evaluation/chat` endpoint returns answer, retrieved chunks, sources, language, intent, and timings
- [x] EV.3 Evaluation endpoint verified against one known dataset question using the real RAG pipeline
- [x] EV.4 Batch runner reads XLSX dataset and calls evaluation endpoint with JSONL resume/caching
- [x] EV.5 RAGAS evaluator configured for `gpt-oss-120b` through Ollama or OpenWebUI
- [x] EV.6 RAGAS scoring script computes retriever metrics: context precision, context recall, context relevance
- [x] EV.7 RAGAS scoring script computes generator metrics: faithfulness, answer relevancy, answer correctness or documented equivalent
- [x] EV.8 Deterministic retrieval diagnostics implemented: file hit@K, page hit@K, expected rank
- [x] EV.9 Summary report generated by overall score, language, source file, original/paraphrase grouping, and failure type
- [x] EV.10 Pilot evaluation run completed on `evaluation/dataset/test_evaluation_dataset.xlsx`
- [ ] EV.11 Full 1000+ question evaluation completed and outputs saved under `evaluation/outputs/`
- [x] EV.12 `evaluation_result.xlsx` generated with `Question Results` and `Dataset Summary` sheets

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
- 2026-06-09: Updated RAG answer generation to stream with LLM temperature `0.1` from `ChatOrchestrator` while leaving classifier/completion calls unchanged. Verified with `dotnet build src\API\PolicyBot.Api.csproj`.
- 2026-06-09: Implemented Phase 4 frontend in `Web/` following the Figma Make two-page structure: Chat and Documents navigation, streaming chat UI, source/image/form-download rendering, document upload with agent selector, and form mapping suggestion review actions. Verified `npm.cmd run build` and Vite dev server HTTP `200` on `http://127.0.0.1:5173`; in-app browser verification was unavailable because the `iab` browser surface was not present.
- 2026-06-09: Verified Phase 4 frontend end-to-end on this workspace using Vite `http://127.0.0.1:5174` and API `http://127.0.0.1:5000` with `LlmProvider__Active=Ollama`. Fixed SSE parsing in `Web/src/api.ts` so CRLF-delimited `[SOURCES]` events are handled separately instead of leaking into assistant text. Rebuilt with `npm.cmd run build`. Browser-verified chat history + streaming greeting, grounded CII Tower answer with source chips and image rendering, upload endpoint success with manual `ELCA_HR` selection (`chunks: 48`), and form mapping review groups/actions (Accept, Reject, Choose another) with confidence scores.
- 2026-06-09: Implemented and verified Enhancement Phase 1 tasks E1.3-E1.5. Added `GET /api/documents`, `DELETE /api/documents/{sourceFile}`, `GET /api/providers/current`, and `GET /api/providers/status`, plus the Documents page indexed-library table, delete controls, provider/model display, and service status chips. Verified `dotnet build src\API\PolicyBot.Api.csproj`, `npm.cmd run build`, provider current/status endpoints with Ollama/TEI/Qdrant healthy, Qdrant document summaries, disposable XLSX ingest appearing in the library (`48` chunks, `24` pages, `hasFormTemplate: true`), delete removing exactly `48` chunks and the row, and browser-rendered Documents UI with provider values, healthy chips, 45 indexed rows, table headers, and document-specific delete buttons.
- 2026-06-09: Refined the Figma-style Documents upload flow. Added `POST /api/ingest/batch`, changed the Documents upload button to open a modal with multi-file selection/drag-drop, wired the frontend upload to the batch API, removed the agent selector from upload, and removed the agent column/search from the document list. Verified `npm.cmd run build`, `dotnet build src\API\PolicyBot.Api.csproj -c Release`, and source search confirming old upload-agent UI references are gone. Debug API build remains blocked while the existing local API process is running.
- 2026-06-09: Removed system status/provider visibility for now per updated UI direction. Deleted `/api/providers/current` and `/api/providers/status`, removed provider response models and `ProviderVisibilityService`, removed the Chat system-status panel and frontend provider fetch code, and moved prompt suggestions into the chat panel above the composer as text-only suggestions with yellow star icons. E1.4/E1.5 are unchecked because the status display is intentionally no longer present.
- 2026-06-09: Improved chat response presentation by rendering assistant markdown for paragraphs, numbered lists, bullet lists, and bold text without raw HTML, and changed downloadable form/file chips to use a download icon. Verified `npm.cmd run build`.
- 2026-06-09: Fixed hybrid retrieval duplicate-key crash when dense or keyword search returns the same chunk key more than once. `HybridSearchService` now groups by source/page/chunk/type and keeps the highest-scoring duplicate before merging. Verified `dotnet build src\API\PolicyBot.Api.csproj -c Release`.
- 2026-06-09: Made Documents tab filenames downloadable when their stored upload can be matched under the configured `data/uploads` folder. `GET /api/documents` now includes `downloadPath`; the frontend renders a filename link only when that path is available. Chat citation chips remain non-downloadable. Verified `dotnet build src\API\PolicyBot.Api.csproj -c Release` and `npm.cmd run build`.
- 2026-06-09: Made chat citation chips downloadable using the same `data/uploads` matching as the Documents tab. `SourceRef` now includes `downloadPath`; citation chips render as links only when a stored upload can be resolved. Verified `dotnet build src\API\PolicyBot.Api.csproj -c Release` and `npm.cmd run build`.
- 2026-06-09: Implemented ingestion deduplication/versioning enhancement from `docs/enhancements/enhancement-phase-2.md`. Added deterministic SHA256-derived Qdrant UUIDs, MD5 file hash metadata, `ingested_at` payloads, existing-document hash lookup, unchanged-file skip behavior, delete-before-upsert for changed files, `force=true` support, and expanded ingest response metadata (`replacedChunks`, `isUpdate`, `skipped`, `fileHash`, `ingestedAt`). Verified compile with `dotnet build src\API\PolicyBot.Api.csproj -c Release` and frontend type build with `npm.cmd run build`; runtime duplicate-upload verification remains to be run against local Qdrant/TEI/API before marking done end-to-end.
- 2026-06-10: Added `evaluation/ragas-evaluation-plan.md` for the RAGAS evaluation phase and added the EV.1-EV.12 checklist to `.claude/PROGRESS.md`. Verified the document exists and the checklist is present; implementation tasks remain unchecked. Updated the plan to require `evaluation_result.xlsx` with `Question Results` and `Dataset Summary` sheets.
- 2026-06-10: Implemented the first evaluation harness pass: `POST /api/evaluation/chat`, evaluation DTOs/orchestrator, topK override in `HybridSearchService`, `run_rag_batch.py`, `run_ragas_eval.py`, and `summarize_results.py`. Verified `dotnet build src\API\PolicyBot.Api.csproj -c Release`, Python syntax checks, XLSX fallback parsing via batch smoke test, and `evaluation_result.xlsx` generation from a smoke-test JSONL. Left EV.2-EV.12 unchecked because the endpoint was not runtime-verified against a live RAG API and RAGAS scoring is blocked locally by missing Python package `openai`.
- 2026-06-10: Added `evaluation/requirements.txt` and pinned the Python 3.12-compatible evaluation stack: `ragas==0.2.15`, LangChain `0.3.x`, OpenAI, datasets, OpenPyXL, and transformers. Verified the live API on `http://localhost:5000`, then ran `evaluation/dataset/test_evaluation_dataset.xlsx` through the full evaluation flow. Batch generation produced 9/9 rows, RAGAS scoring completed all 6 requested metrics with 0 row errors, and `evaluation/outputs/test-evaluation-dataset/evaluation_result.xlsx` was generated with `Question Results` and `Dataset Summary` sheets. Marked EV.2-EV.10 and EV.12 complete; EV.11 remains unchecked until the full 1000+ question dataset is run.
- 2026-06-10: Added `evaluation/run_evaluation.py` as a one-command wrapper that takes an input XLSX and output XLSX, then runs batch answer generation, RAGAS scoring, and workbook summarization. Verified with a one-row smoke run: `evaluation/outputs/wrapper-smoke/evaluation_result.xlsx` contains `Question Results` and `Dataset Summary`.
- 2026-06-11: Added `evaluation/test_reranker_multilingual.py` plus reranker config/docs to smoke-test whether a hosted `bge-reranker` ranks the expected chunk first for EN, VI, FR, and DE queries before wiring it into dense-only retrieval. Verified Python syntax and CLI help locally; live reranker verification requires the company reranker URL/API shape.
- 2026-06-11: Tested company `inference-bge-reranker` host. Native rerank attempts against `/api/rerank`, `/api/`, `/api/v1/rerank`, and `/v1/rerank` returned `405 Method Not Allowed`. OpenWebUI model listing shows `inference-bge-reranker`, but calling it through `/api/v1/chat/completions` returned `404`/`429` for that model group, so the live multilingual reranker test is still blocked on the correct native rerank endpoint or backend route.
 
