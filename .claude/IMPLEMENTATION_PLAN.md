# ELCA Policy Chatbot — Phased Implementation Plan

> **How to use this document:**
> - Each phase is broken into subtasks sized for a junior developer
> - At the end of each coding session, the AI agent verifies completed work and checks the corresponding checkboxes
> - When starting a new session, feed this file to Claude Code — it will read the checkboxes and continue from where you left off
> - Do not mark a checkbox unless the feature is verified working end-to-end

---

## Project Overview

| Field | Detail |
|---|---|
| Project name | ELCA Policy Chatbot |
| Objective | Chatbot for employees to query company policies in EN, VI, FR, DE |
| Backend | .NET 10 / ASP.NET Core |
| Vector DB | Qdrant (Docker) |
| Embedding | BAAI/bge-m3 via HuggingFace TEI (Docker) |
| LLM | Llama 3.3 70B via company OpenWebUI (switchable to Ollama) |
| Frontend | React JS |
| MCP | ModelContextProtocol NuGet |

---

## Session Checklist (master progress tracker)

> Agent: after verifying each phase, update the checkboxes below.

### Phase 1 — Infrastructure & project setup
- [ ] 1.1 Docker environment running (Qdrant + TEI)
- [ ] 1.2 Qdrant collection created
- [ ] 1.3 ASP.NET Core project scaffolded
- [ ] 1.4 DI, config, and folder structure in place
- [ ] 1.5 LLM provider abstraction implemented
- [ ] 1.6 EmbeddingService connected to TEI
- [ ] 1.7 VectorStoreService connected to Qdrant

### Phase 2 — Document ingestion pipeline
- [ ] 2.1 PDF parser (text + image extraction)
- [ ] 2.2 PPTX → PDF conversion + pipeline
- [ ] 2.3 DOCX parser (text + image extraction)
- [ ] 2.4 DOC → DOCX conversion + pipeline
- [ ] 2.5 XLSX parser (form field reconstruction)
- [ ] 2.6 Text chunker — Strategy A: fixed-size (400 tokens, 80 overlap)
- [ ] 2.7 Text chunker — Strategy B: paragraph/semantic boundary
- [ ] 2.8 Text chunker — Strategy C: sliding window with sentence awareness
- [ ] 2.9 Chunker evaluation: compare strategies on real documents, pick best
- [ ] 2.10 Document classifier — manual selection via API param
- [ ] 2.11 Document classifier — LLM auto-classify fallback
- [ ] 2.12 Full ingestion orchestrator wired end-to-end
- [ ] 2.13 POST /api/ingest endpoint (with optional `agent` param)
- [ ] 2.14 Static image serving configured

### Phase 3 — RAG query pipeline
- [ ] 3.1 Language detection service
- [ ] 3.2 Intent classifier (hybrid fast-path + LLM)
- [ ] 3.3 Intent response templates (all 4 languages)
- [ ] 3.4 Vector search (embed query → search Qdrant → filter by score)
- [ ] 3.5 Prompt builder (context injection + language instruction + citation format)
- [ ] 3.6 LLM streaming service
- [ ] 3.7 Source citation parser (extracts filename + page from LLM response)
- [ ] 3.8 POST /api/chat SSE endpoint
- [ ] 3.9 End-to-end RAG verified in all 4 languages

### Phase 4 — Frontend
- [ ] 4.1 React project scaffolded
- [ ] 4.2 Chat UI with message history
- [ ] 4.3 SSE streaming rendering (tokens appear in real time)
- [ ] 4.4 Source citation panel (filename + page chips)
- [ ] 4.5 Image display alongside citations
- [ ] 4.6 Document upload UI (with optional agent selector dropdown)

### Phase 5 — Polish & demo prep
- [ ] 5.1 Error handling (LLM timeout, empty results, unsupported file type)
- [ ] 5.2 Multilingual tested end-to-end (EN, VI, FR, DE)
- [ ] 5.3 Citation accuracy verified with real policy files
- [ ] 5.4 Demo script prepared (3–4 questions per language)
- [ ] 5.5 README written (setup, docker-compose, ingest steps)

### Phase 6 — Agent orchestration & MCP *(additional, after core is working)*
- [ ] 6.1 Router agent (classifies query to ELCA_HR / ELCA_GENERAL / CII_TOWER_SUPPORT)
- [ ] 6.2 Vector search updated to filter by agent domain
- [ ] 6.3 Tool-calling loop (search_policy, list_documents, ingest_document)
- [ ] 6.4 MCP server configured (ModelContextProtocol NuGet)
- [ ] 6.5 MCP tools exposed and tested with external tool call

---

## Phase 1 — Infrastructure & project setup

**Goal:** All services running, .NET project scaffolded, core abstractions in place.

### 1.1 Docker environment

**Status:** `[ ]`

Verify both containers are running and healthy:

```bash
curl http://localhost:6333/healthz      # expected: {"title":"qdrant - healthy"}
curl http://localhost:8080/health       # expected: OK
```

Test Vietnamese embedding:
```bash
curl http://localhost:8080/embed -X POST -H "Content-Type: application/json" -d "{\"inputs\": [\"Chính sách nghỉ phép hàng năm là gì?\"]}"
```
Expected: JSON array of 1024 floats.

### 1.2 Qdrant collection

**Status:** `[ ]`

Create the `policy_docs` collection:
```bash
curl -X PUT http://localhost:6333/collections/policy_docs -H "Content-Type: application/json" -d "{\"vectors\": {\"size\": 1024, \"distance\": \"Cosine\"}}"
```
Expected: `{"result":true,"status":"ok"}`

### 1.3 ASP.NET Core project scaffold

**Status:** `[ ]`

Create the following folder structure inside `src/API/`:

```
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
    ParsedChunk.cs
    ChatRequest.cs
    ChatResponse.cs
    SourceRef.cs
  appsettings.json
  Program.cs
```

### 1.4 DI, config, and folder structure

**Status:** `[ ]`

`appsettings.json` must include:

```json
{
  "LlmProvider": {
    "Active": "OpenWebUI",
    "OpenWebUI": {
      "BaseUrl": "https://your-company-openwebui.com",
      "ApiKey": "your-key",
      "Model": "llama33-70b"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.3"
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
    "TempPath": "../../data/temp"
  }
}
```

### 1.5 LLM provider abstraction

**Status:** `[ ]`

```csharp
// Providers/ILlmProvider.cs
public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens = 1000);
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

`OpenAICompatibleProvider` implements `ILlmProvider` using `/v1/chat/completions` — works for both OpenWebUI and Ollama since both are OpenAI-compatible.

Register in `Program.cs` based on `LlmProvider:Active` config value — switching provider requires only changing `appsettings.json`, no code change.

### 1.6 EmbeddingService

**Status:** `[ ]`

- HTTP client calling `POST http://localhost:8080/embed`
- Accepts `List<string>`, returns `List<float[]>`
- Batch size 32 (configurable)

### 1.7 VectorStoreService

**Status:** `[ ]`

- Connect to Qdrant using `Qdrant.Client` NuGet
- Implement `UpsertAsync(List<ParsedChunk>, List<float[]>)`
- Implement `SearchAsync(float[] vector, string agent, int limit)` with agent filter
- Minimum similarity score filter: `0.45f`

**Verification:** upsert a test point, search for it, confirm it returns.

---

## Phase 2 — Document ingestion pipeline

**Goal:** All file types parsed, chunked, classified, embedded, and stored in Qdrant with correct metadata including image paths.

### Core data model

```csharp
public class ParsedChunk
{
    public string Text { get; set; }
    public string SourceFile { get; set; }
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string FileType { get; set; }        // "pdf" | "docx" | "xlsx"
    public string Agent { get; set; }           // "ELCA_HR" | "ELCA_GENERAL" | "CII_TOWER_SUPPORT"
    public List<string> ImagePaths { get; set; } = [];
}
```

### Qdrant payload schema

```json
{
  "source_file": "leave_policy.pdf",
  "page": 3,
  "chunk_index": 1,
  "file_type": "pdf",
  "agent": "ELCA_HR",
  "image_paths": "/images/leave_policy/page3_img0.png"
}
```

### 2.1 PDF parser

**Status:** `[ ]`

Library: `UglyToad.PdfPig`

- Extract text per page using `page.GetWords()`
- Extract images per page using `page.GetImages()`
- Skip images smaller than 100×100 pixels (decorative)
- Save images to `/data/images/{docName}/page{N}_img{I}.png`
- Store URL-friendly relative path in `ImagePaths`
- Attach all page images to all chunks from that page

**Verification:** parse a PDF with images, confirm text extracted and images saved to disk.

### 2.2 PPTX → PDF conversion

**Status:** `[ ]`

Library: LibreOffice headless (must be installed in environment)

```bash
libreoffice --headless --convert-to pdf --outdir {outputDir} file.pptx
```

- Each slide becomes one page
- Slide number = page number in citations
- After conversion, route through PDF parser (2.1)

**Verification:** convert a PPTX, confirm output PDF has correct page count matching slide count.

### 2.3 DOCX parser

**Status:** `[ ]`

Library: `DocumentFormat.OpenXml`

- Extract paragraphs using `body.Elements<Paragraph>()`
- Track page breaks via `Break` elements to estimate page number
- Extract images via `ImagePart` relationships (document-level, not per-page)
- Attach all document images to all chunks
- Filter empty paragraphs

**Verification:** parse a DOCX with images, confirm paragraphs and images extracted correctly.

### 2.4 DOC → DOCX conversion

**Status:** `[ ]`

Library: LibreOffice headless

```bash
libreoffice --headless --convert-to docx --outdir {outputDir} file.doc
```

After conversion, route through DOCX parser (2.3).

### 2.5 XLSX parser

**Status:** `[ ]`

Library: `EPPlus` (free for non-commercial) or `ClosedXML` (MIT, confirm with team)

- Do NOT dump raw cell values
- Reconstruct each row as readable prose:
  `"Field: {col1} — Value: {col2} — Description: {col3}"`
- Treat each sheet as one page
- No image extraction needed for forms
- Filter empty rows

**Verification:** parse an internal purchasing form, confirm output reads as natural sentences not raw cell dumps.

### 2.6 Text chunker — Strategy A: fixed-size

**Status:** `[ ]`

The baseline approach. Implement first, evaluate later.

- Split by word count: 400 words per chunk, 80 word overlap
- Filter chunks under 30 words (headers, footers, page numbers)
- All page images travel with every chunk from that page

### 2.7 Text chunker — Strategy B: paragraph/semantic boundary

**Status:** `[ ]`

Respect natural document structure instead of cutting mid-sentence.

- Split on double newlines (`\n\n`) or heading patterns first
- If a paragraph exceeds 400 words, fall back to fixed-size splitting within it
- Produces more coherent chunks — better for policy documents with clear sections

### 2.8 Text chunker — Strategy C: sliding window with sentence awareness

**Status:** `[ ]`

Most sophisticated approach — split on sentence boundaries only.

- Use sentence detection (split on `.`, `?`, `!`, `。`) to find clean boundaries
- Build chunks by accumulating sentences until token limit reached
- Overlap by carrying the last 1–2 sentences into the next chunk
- Preserves meaning at boundaries better than fixed word splits

### 2.9 Chunker evaluation

**Status:** `[ ]`

After implementing all three strategies, evaluate on real policy documents:

- Ingest the same document with each strategy into separate test collections
- Ask 5 representative questions, compare retrieval quality
- Check: are retrieved chunks coherent? Do they contain the full answer context?
- Pick the best strategy (or make it configurable per file type) before full ingestion

> **Note:** This is worth spending time on. Chunking quality directly determines retrieval quality — a well-chunked document with a simple retriever outperforms a poorly-chunked document with a sophisticated retriever.

Configurable in `appsettings.json`:
```json
{
  "Ingestion": {
    "ChunkingStrategy": "ParagraphBoundary"
  }
}
```

### 2.10 Document classifier — manual selection

**Status:** `[ ]`

Allow the caller to specify the agent tag explicitly at upload time. This is the primary path when the user knows which domain the document belongs to.

API endpoint accepts an optional `agent` query parameter:

```
POST /api/ingest?agent=ELCA_HR
```

Valid values: `ELCA_HR`, `ELCA_GENERAL`, `CII_TOWER_SUPPORT`

If `agent` is provided → skip LLM classification, apply directly to all chunks.

Frontend upload UI shows a dropdown:
```
[ Auto-detect ▼ ]
  Auto-detect
  ELCA HR
  ELCA General
  CII Tower Support
```

### 2.11 Document classifier — LLM auto-classify fallback

**Status:** `[ ]`

Used when user selects "Auto-detect" or omits the `agent` param.

Send first 500 words to LLM:

```
System:
You are a document classifier for ELCA company.
Classify the document excerpt into exactly one category:
- ELCA_HR: HR policies, leave, recruitment, benefits, employee conduct, salary
- ELCA_GENERAL: Company general policies, IT, security, operations, finance
- CII_TOWER_SUPPORT: CII Tower building, facility, support services, maintenance
Respond with ONLY the category name. Nothing else.

User: {first500Words}
```

- One LLM call per document
- Apply tag to all chunks from that document
- Fallback to `ELCA_GENERAL` if LLM returns unexpected value
- Log classification result so user can verify it was correct

### 2.12 Full ingestion orchestrator

**Status:** `[ ]`

`DocumentIngestionService` decision flow:

```
File uploaded + optional agent param
         ↓
agent param provided?
   YES → skip LLM, use provided agent tag
   NO  → call LLM classifier on first 500 words
         ↓
Convert if needed (DOC→DOCX, PPTX→PDF)
         ↓
Parse + extract images
         ↓
Chunk with selected strategy
         ↓
Apply agent tag to all chunks
         ↓
Embed in batches of 32
         ↓
Upsert to Qdrant with full payload
```

### 2.13 POST /api/ingest endpoint

**Status:** `[ ]`

```
POST /api/ingest?agent=ELCA_HR   (optional agent param)
Body: multipart/form-data with file
```

- Allowed extensions: `.pdf`, `.docx`, `.doc`, `.xlsx`, `.pptx`
- Returns `{ "message": "{filename} ingested successfully", "agent": "ELCA_HR", "chunks": 42 }`
- Returns `400` for unsupported file types

**Verification:** upload each file type with and without `agent` param. Open Qdrant dashboard at `http://localhost:6333/dashboard` and confirm chunks appear with correct `agent` and `image_paths` metadata.

### 2.14 Static image serving

**Status:** `[ ]`

Add to `Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});
```

**Verification:** upload a PDF with images, access `/images/{docName}/page1_img0.png` in browser, confirm image loads.

---

## Phase 3 — RAG query pipeline

**Goal:** Working end-to-end RAG — user asks a policy question, gets a grounded answer in their language with source citations. No agent routing yet — all documents searched together.

### 3.1 Language detection service

**Status:** `[ ]`

Library: `NTextCat`

- Detect language from user message
- Return ISO code: `en`, `vi`, `fr`, `de`
- Default to `en` if detection fails or language unsupported

### 3.2 Intent classifier

**Status:** `[ ]`

Hybrid approach — fast path first, LLM fallback for ambiguous messages.

**Fast path (no LLM call):**
1. Exact match against smalltalk keywords (hi, hello, chào, bonjour, hallo, cảm ơn, merci, danke…)
2. Message length < 10 characters → `SMALLTALK`
3. Contains policy keywords (policy, leave, nghỉ phép, congé, urlaub…) → `POLICY_QUERY`

**LLM fallback prompt:**
```
Classify the user message into exactly one category:
- SMALLTALK: greetings, thanks, casual conversation
- POLICY_QUERY: any question about company policies, HR, leave, overtime,
  salary, benefits, IT, facilities, procedures, forms, CII Tower
- OUT_OF_SCOPE: questions unrelated to company policies

When in doubt between POLICY_QUERY and OUT_OF_SCOPE, choose POLICY_QUERY.
Respond with ONLY the category name.

User message: {message}
```

- `maxTokens: 10`
- Default to `POLICY_QUERY` if LLM returns unexpected value

### 3.3 Intent response templates

**Status:** `[ ]`

Hardcode in a static `IntentResponses` class — do not ask LLM to generate these:

- `Smalltalk[lang]` — array of friendly greeting responses, pick randomly
- `OutOfScope[lang]` — polite refusal explaining the bot only knows company policies
- `NoResults[lang]` — message shown when Qdrant returns no chunks above threshold

All four languages: `en`, `vi`, `fr`, `de`.

### 3.4 Vector search

**Status:** `[ ]`

- Embed user query via `EmbeddingService`
- Search Qdrant across **all documents** (no agent filter in Phase 3)
- Return top 6 chunks
- Filter by minimum similarity score `0.45f`
- If 0 results after filter → return `NoResults` response

### 3.5 Prompt builder

**Status:** `[ ]`

```
You are a helpful assistant for ELCA company policy questions.
Answer ONLY based on the provided context. Do not use outside knowledge.
Always respond in the same language as the user's question.
Supported languages: English (en), Vietnamese (vi), French (fr), German (de).

After your answer, list sources in this exact format:
SOURCES: filename.pdf (page N), filename2.docx (page M)

Context:
{retrievedChunks}

Question: {userQuery}
```

### 3.6 LLM streaming service

**Status:** `[ ]`

- Call active `ILlmProvider.StreamAsync`
- Return `IAsyncEnumerable<string>` of tokens
- Works for both OpenWebUI and Ollama (both OpenAI-compatible)

### 3.7 Source citation parser

**Status:** `[ ]`

- Parse `SOURCES:` line from end of LLM response
- Extract into `List<SourceRef>` with `File`, `Page`, `ImagePaths`
- Look up `ImagePaths` from Qdrant payload of the matched chunks

### 3.8 POST /api/chat SSE endpoint

**Status:** `[ ]`

```csharp
Response.Headers.Append("Content-Type", "text/event-stream");
Response.Headers.Append("Cache-Control", "no-cache");

await foreach (var token in _orchestrator.StreamAsync(request, ct))
{
    await Response.WriteAsync($"data: {token}\n\n", ct);
    await Response.Body.FlushAsync(ct);
}
```

Final SSE event sends structured sources JSON separately.

### 3.9 End-to-end RAG verification

**Status:** `[ ]`

Test matrix — ask the same question in all 4 languages:

| Language | Test question |
|---|---|
| English | "What is the annual leave policy?" |
| Vietnamese | "Chính sách nghỉ phép hàng năm là gì?" |
| French | "Quelle est la politique de congé annuel?" |
| German | "Was ist die Richtlinie für den Jahresurlaub?" |

Verify for each:
- Response language matches question language ✓
- Answer is grounded in document content ✓
- `SOURCES:` line present with correct filename + page ✓
- Images display if source page has images ✓

---

## Phase 4 — Frontend

**Goal:** Working chat UI with streaming, citation display, image rendering, and document upload with agent selector.

### 4.1 React project scaffold

**Status:** `[ ]`

```bash
npm create vite@latest web -- --template react-ts
cd web && npm install axios
```

### 4.2 Chat UI

**Status:** `[ ]`

- Message input (text field + send button)
- Conversation history (alternating user / bot messages)
- Loading indicator while waiting for first token

### 4.3 SSE streaming rendering

**Status:** `[ ]`

```javascript
const eventSource = new EventSource('/api/chat');
eventSource.onmessage = (e) => {
  setCurrentMessage(prev => prev + e.data);
};
```

Tokens must render as they arrive — not all at once after completion.

### 4.4 Source citation panel

**Status:** `[ ]`

- Show below each bot message
- Display as chips: `📄 leave_policy.pdf — Page 3`

### 4.5 Image display

**Status:** `[ ]`

- If `source.image_paths` is not empty, render `<img>` tags below the citation chip
- Images served from `/images/` static path
- Max width: 100% of chat panel

### 4.6 Document upload UI

**Status:** `[ ]`

- File picker (accepted: `.pdf`, `.docx`, `.doc`, `.xlsx`, `.pptx`)
- **Agent selector dropdown:**
  - Auto-detect (default)
  - ELCA HR
  - ELCA General
  - CII Tower Support
- POST to `/api/ingest?agent={selected}` (omit param if Auto-detect)
- Show loading state during processing
- Show success message with chunk count on completion

**Verification:** upload with Auto-detect, confirm agent tag in Qdrant matches expected domain. Upload with manual selection, confirm tag is applied correctly.

---

## Phase 5 — Polish & demo prep

**Goal:** System is stable, tested with all real documents, demo-ready.

### 5.1 Error handling

**Status:** `[ ]`

Handle gracefully (no 500 errors, always a user-friendly message):
- LLM API timeout or unavailable
- TEI embedding service down
- Qdrant connection failed
- Unsupported file type uploaded
- Document parsing failure (corrupt file, empty text)
- Empty retrieval results (handled by NoResults response)

### 5.2 Multilingual end-to-end test

**Status:** `[ ]`

- Response language always matches question language
- Citations correct across all 4 languages
- Images display when applicable

### 5.3 Citation accuracy

**Status:** `[ ]`

- Upload all 20 company policy files
- Ask 5+ questions covering each knowledge domain
- Verify every citation points to correct file and page number

### 5.4 Demo script

**Status:** `[ ]`

Prepare and rehearse:
1. Upload one PDF and one XLSX form (show auto-classification working)
2. Ask a policy question in English → show streaming answer + citation + image
3. Ask the same question in Vietnamese → same citation, Vietnamese response
4. Ask an unrelated question → show polite refusal (no Qdrant call)
5. Say "Hi" → show smalltalk response
6. Upload a document with manual agent selection

### 5.5 README

**Status:** `[ ]`

Must cover:
- Prerequisites (Docker Desktop, WSL, .NET 10, Node.js)
- `docker compose up -d` to start Qdrant + TEI
- How to create Qdrant collection
- How to run the API
- How to run the frontend (`npm run dev`)
- How to ingest documents (UI or curl)
- How to switch LLM provider (`appsettings.json`)

---

## Phase 6 — Agent orchestration & MCP *(additional — start only after Phase 5 is complete)*

> **Prerequisites:** Core RAG pipeline fully working and demo-ready. Do not start this phase until Phase 5 checklist is 100% checked.

**Goal:** Add router agent for domain-aware retrieval, expose backend as MCP server.

### 6.1 Router agent

**Status:** `[ ]`

LLM prompt at query time:
```
Classify the following question into exactly one category:
- ELCA_HR
- ELCA_GENERAL
- CII_TOWER_SUPPORT
Respond with ONLY the category name.

Question: {userQuery}
```

- `maxTokens: 10`
- Default to `ELCA_GENERAL` if unexpected value
- Applied after intent classification, before vector search

### 6.2 Vector search updated with agent filter

**Status:** `[ ]`

- Add agent filter to Qdrant search (was searching all docs in Phase 3)
- Only chunks tagged with the router's output agent are searched
- Verify routing is correct for at least 2 questions per domain

### 6.3 Tool-calling loop

**Status:** `[ ]`

> Test Llama 3.3 70B tool calling reliability first. If unreliable due to quantization, keep prompt-based routing from 6.1 and skip this subtask.

Tools:
| Tool | Description |
|---|---|
| `search_policy` | Search Qdrant by query + optional agent filter |
| `list_documents` | List ingested files with agent tags |
| `ingest_document` | Ingest a document from file path |

### 6.4 MCP server

**Status:** `[ ]`

NuGet: `ModelContextProtocol`

```csharp
builder.Services.AddMcpServer().WithHttpTransport();
```

Expose `search_policy`, `list_documents`, `ingest_document` as MCP tools.

### 6.5 MCP tested

**Status:** `[ ]`

Call MCP tools externally, confirm correct responses.

---

## Known risks

| Risk | Level | Mitigation |
|---|---|---|
| Company LLM API does not support streaming | 🔴 High | Test in Phase 1 — if no streaming, use polling fallback |
| Vietnamese diacritics garbled in older PDFs | 🔴 High | Test with actual files in Phase 2 — add Tesseract OCR fallback |
| Chunking strategy produces poor retrieval quality | 🟡 Med | Phase 2.9 evaluation step — test all 3 strategies before full ingestion |
| Complex tables/layouts in real policy files | 🟡 Med | Phase 5 buffer for fixing parsing edge cases |
| Llama 3.3 70B tool calling unreliable (Phase 6) | 🟡 Med | Test before building — fall back to prompt-based routing if needed |
| EPPlus license (non-commercial) | 🟡 Med | Confirm with team — swap to ClosedXML (MIT) if needed |
| bge-m3 TEI slow on CPU | 🟢 Low | Reduce batch size or use GPU if available |

---

## Out of scope

- Scanned PDFs / OCR (flagged as risk, not in v1)
- Image content search (no CLIP / vision processing)
- Authentication / user login
- Per-user conversation history
- Cloud deployment
- Document access control
- Real-time folder watching / auto-sync
- Languages outside EN, VI, FR, DE
- Fine-tuning or feedback learning