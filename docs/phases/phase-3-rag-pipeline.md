# Phase 3 — RAG Query Pipeline

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: implement the full RAG pipeline — user message → intent check → language detect → embed → search Qdrant → build prompt → stream answer with citations. No agent routing yet — all documents searched together.

---

## Query pipeline flow

```
User message
      ↓
Language detection
      ↓
Intent classification (hybrid fast-path + LLM)
      ↓
┌─────────────┬──────────────────┬───────────────┐
SMALLTALK     POLICY_QUERY       OUT_OF_SCOPE
      ↓              ↓                  ↓
Hardcoded      Embed query        Hardcoded
response       → Qdrant search    refusal
               → filter score
               → Prompt builder
               → LLM stream
               → Parse citations
               → Return answer + sources
```

---

## Task 3.1 — Language detection

Implement `LanguageDetectionService` using `NTextCat`.

- Detect language from user message
- Return ISO code: `en`, `vi`, `fr`, `de`
- Default to `en` if detection fails or language is not in supported set

---

## Task 3.2 — Intent classifier (fast path)

Implement the fast path in `IntentClassifierService` — no LLM call needed for obvious cases.

Check in order:

1. **Exact smalltalk match** (case-insensitive, trimmed):
   ```
   EN: hi, hello, hey, thanks, thank you, bye, goodbye, ok, okay, sure, great
   VI: chào, xin chào, cảm ơn, tạm biệt, ổn, được, vâng, dạ
   FR: bonjour, salut, merci, au revoir, bonsoir, d'accord
   DE: hallo, guten tag, danke, tschüss, auf wiedersehen, gut
   ```
   → return `SMALLTALK`

2. **Message too short**: `message.Trim().Length < 10` → return `SMALLTALK`

3. **Contains policy keywords**:
   ```
   EN: policy, policies, leave, annual, sick, overtime, salary, benefit,
       allowance, procedure, form, request, approval, hr, facility, cii, tower
   VI: chính sách, nghỉ phép, lương, phúc lợi, quy trình, tăng ca, biểu mẫu
   FR: politique, congé, salaire, avantage, procédure, formulaire
   DE: richtlinie, urlaub, gehalt, verfahren, überstunden, formular
   ```
   → return `POLICY_QUERY`

---

## Task 3.3 — Intent classifier (LLM fallback)

If fast path returns no result, call LLM:

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

Rules:
- `maxTokens: 10`
- Default to `POLICY_QUERY` if response is unexpected

---

## Task 3.4 — Intent response templates

Create static `IntentResponses` class with hardcoded strings — do not generate these via LLM.

Three response types, all 4 languages:

**Smalltalk** (array — pick randomly):
```csharp
["en"] = [
  "Hello! I'm ELCA's policy assistant. Feel free to ask me anything about company policies.",
  "Hi there! How can I help you with company policies today?"
]
["vi"] = [
  "Xin chào! Tôi là trợ lý chính sách của ELCA. Bạn có thể hỏi tôi về các chính sách công ty.",
  "Chào bạn! Tôi có thể giúp gì cho bạn về chính sách và quy trình của công ty?"
]
// same for fr, de
```

**OutOfScope** (single string per language):
```
EN: "I can only answer questions about ELCA company policies and procedures. I don't have information about that topic."
VI: "Tôi chỉ có thể trả lời các câu hỏi về chính sách và quy trình của công ty ELCA. Tôi không có thông tin về chủ đề này."
FR: "Je ne peux répondre qu'aux questions sur les politiques et procédures d'ELCA."
DE: "Ich kann nur Fragen zu ELCA-Unternehmensrichtlinien beantworten."
```

**NoResults** (when Qdrant returns nothing above threshold):
```
EN: "I couldn't find relevant information in the company documents for your question. Try rephrasing."
VI: "Tôi không tìm thấy thông tin liên quan trong tài liệu công ty. Hãy thử diễn đạt lại câu hỏi."
FR: "Je n'ai pas trouvé d'informations pertinentes. Essayez de reformuler votre question."
DE: "Ich konnte keine relevanten Informationen finden. Versuchen Sie, die Frage umzuformulieren."
```

---

## Task 3.5 — Vector search

In `VectorStoreService.SearchAsync`:

- Embed user query via `EmbeddingService`
- Search Qdrant with `agent = null` (no filter in Phase 3 — search all documents)
- Return top 6 results
- Filter by score `>= 0.45f`
- If 0 results remain → caller returns `NoResults` response

Both `chunk_type: "text"` and `chunk_type: "image_caption"` chunks are eligible for retrieval.

---

## Task 3.6 — Prompt builder

`PromptBuilderService.Build(string userQuery, IReadOnlyList<ScoredChunk> chunks, string language)`:

```
You are a helpful assistant for ELCA company policy questions.
Answer ONLY based on the provided context. Do not use outside knowledge.
Always respond in the same language as the user's question.
Supported languages: English (en), Vietnamese (vi), French (fr), German (de).

After your answer, list the sources on a new line in this exact format:
SOURCES: filename.pdf (page N), filename2.docx (page M)

Context:
{chunks joined with double newline}

Question: {userQuery}
```

---

## Task 3.7 — LLM streaming service

`LlmService.StreamAsync(string prompt, CancellationToken ct)`:

- Call `ILlmProvider.StreamAsync`
- Yield tokens as `IAsyncEnumerable<string>`
- Works for both OpenWebUI and Ollama (both OpenAI-compatible)

Also implement `LlmService.CompleteAsync(string prompt, int maxTokens)` — non-streaming single-turn call used by classifiers.

---

## Task 3.8 — Source citation parser

After streaming completes, extract citations from the full response:

- Find line starting with `SOURCES:`
- Parse entries like `filename.pdf (page 3)`
- For each citation, look up matching chunks from the search results
- Build `List<SourceRef>` with `File`, `Page`, `ChunkType`, `ImagePath`, `FormDownload`
- If chunk is `image_caption` → populate `ImagePath`
- If chunk has `is_form_template: true` → call `FormRegistryService.FindDownload()` to populate `FormDownload`

Updated `SourceRef` model:

```csharp
public class SourceRef
{
    public string File { get; set; }
    public int Page { get; set; }
    public string ChunkType { get; set; }      // "text" or "image_caption"
    public string ImagePath { get; set; }       // populated for image_caption chunks
    public FormDownloadRef? FormDownload { get; set; }  // populated for form templates
}

public class FormDownloadRef
{
    public string FormName { get; set; }        // e.g. "Payment Request Form"
    public string DownloadPath { get; set; }    // e.g. "/templates/Payment_request_form.docx"
}
```

---

## Task 3.9 — FormRegistryService

Implement `Services/Query/FormRegistryService.cs`.

Loads `data/form-registry.json` at startup and provides lookup by filename or alias.

```csharp
public class FormRegistryService
{
    // Load registry from data/form-registry.json at startup
    public void Load(string registryPath);

    // Look up by source filename — used when a retrieved chunk has is_form_template: true
    public FormDownloadRef? FindByFile(string sourceFile);

    // Look up by alias — used to match LLM answer text mentioning a form name
    public FormDownloadRef? FindByAlias(string text);
}
```

Rules:
- Load once at startup via `IHostedService` or in `Program.cs`
- `FindByAlias` does case-insensitive substring matching against all aliases
- If `form-registry.json` is missing or malformed, log a warning and continue — do not crash startup
- Register as singleton in DI

---

## Task 3.10 — Form download enrichment in ChatOrchestrator

After citation parsing, enrich each `SourceRef` with form download info if applicable.

Two enrichment paths:

**Path A — chunk tagged as form template:**
```csharp
if (chunk.Payload["is_form_template"] == true)
{
    sourceRef.FormDownload = _formRegistry.FindByFile(chunk.Payload["source_file"]);
}
```

**Path B — LLM answer text mentions a form name:**
```csharp
var download = _formRegistry.FindByAlias(fullAnswerText);
if (download != null && !sources.Any(s => s.FormDownload?.FormName == download.FormName))
{
    // Append a standalone download ref not tied to a specific source chunk
    downloadRefs.Add(download);
}
```

Both paths can fire on the same response — Path A is more precise, Path B catches mentions in the answer text that weren't in retrieved chunks.

---

## Task 3.11 — POST /api/chat SSE endpoint

```csharp
[HttpPost("chat")]
public async Task Chat([FromBody] ChatRequest request, CancellationToken ct)
{
    Response.Headers.Append("Content-Type", "text/event-stream");
    Response.Headers.Append("Cache-Control", "no-cache");

    await foreach (var token in _orchestrator.StreamAsync(request, ct))
    {
        await Response.WriteAsync($"data: {token}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
```

Final SSE event sends structured sources JSON including form downloads:

```
data: [SOURCES]{
  "sources": [
    {
      "file": "Payment_process_V2_2023.pdf",
      "page": 5,
      "chunkType": "text",
      "imagePath": "",
      "formDownload": {
        "formName": "Payment Request Form",
        "downloadPath": "/templates/Payment_request_form.docx"
      }
    }
  ]
}
```

Frontend listens for `[SOURCES]` prefix to separate stream from citations.

---

## Task 3.12 — End-to-end verification

Test matrix — same question in all 4 languages:

| Language | Test question |
|---|---|
| English | "What is the annual leave policy?" |
| Vietnamese | "Chính sách nghỉ phép hàng năm là gì?" |
| French | "Quelle est la politique de congé annuel?" |
| German | "Was ist die Richtlinie für den Jahresurlaub?" |

Verify for each:
- Response language matches question language
- Answer is grounded in document content (not hallucinated)
- `SOURCES` present with correct filename and page
- Image caption chunks surface with populated `ImagePath`

Also verify form download flow:
- Ask "How do I submit a payment request?" → answer includes citation + `formDownload` with download path
- Confirm `/templates/Payment_request_form.docx` is downloadable from the returned path

Also test intent handling:
- "Hi" → `SMALLTALK` response, no Qdrant call
- "What is the capital of France?" → `OUT_OF_SCOPE` response
- Question with no matching docs → `NoResults` response

---

## Enhancement Phase 3.1 — Hybrid retrieval

Current Phase 3 retrieval uses dense vector search only. Dense retrieval is strong for semantic matching, but it can miss exact identifiers, form names, policy codes, acronyms, article numbers, and bilingual legal/form wording.

Add hybrid retrieval so the query pipeline can combine semantic vector search with keyword/lexical matching.

Why this matters:
- Dense search helps with meaning-level matches, for example “seniority leave entitlement” matching “additional annual leave days”.
- Keyword search helps with exact terms, for example `SCH-HR-003`, `Payment Request Form`, `GIẤY ĐỀ NGHỊ THANH TOÁN`, `CII Tower`, `Article 4`, and form/template names.
- `bge-m3` supports hybrid retrieval concepts: dense embeddings for semantic similarity and sparse lexical representations for keyword-style matching. If the active embedding service exposes sparse vectors later, Qdrant can store sparse vectors directly.

### Enhancement 3.1 tasks

#### Task 3.H1 — Retrieval configuration

Add query retrieval options:

```json
{
  "HybridSearch": {
    "DenseWeight": 0.7,
    "KeywordWeight": 0.3,
    "ExactMatchKeywordWeight": 0.5,
    "Limit": 6,
    "CandidateLimit": 20,
    "MinimumScore": 0.45
  }
}
```

Runtime retrieval always uses hybrid search. Dense-only and keyword-only results are kept only for diagnostics and reporting through verification endpoints.

#### Task 3.H2 — Keyword search service

Implement `KeywordSearchService`.

Initial pragmatic version:
- Search over Qdrant payload text/source metadata, or a lightweight in-memory lexical index built from stored chunks.
- Match normalized query terms against:
  - `text`
  - `source_file`
  - `chunk_type`
  - `file_type`
  - `template_path`
- Boost exact phrase matches and identifier-like tokens.

Identifier-like tokens include:
- policy codes such as `SCH-HR-003`
- form names such as `Payment Request Form`
- Vietnamese form titles such as `GIẤY ĐỀ NGHỊ THANH TOÁN`
- article/page references such as `Article 4`
- acronyms such as `CII`, `PCCC`, `HR`

#### Task 3.H3 — Hybrid search service

Implement `HybridSearchService`.

Flow:
1. Run dense vector search with a larger candidate limit.
2. Run keyword search with the same candidate limit.
3. Merge candidates by stable chunk identity: `source_file + page + chunk_index + chunk_type`.
4. Normalize dense and keyword scores to `0..1`.
5. Compute final score:

```text
final_score = DenseWeight * dense_score + KeywordWeight * keyword_score
```

If the query contains identifier-like tokens or exact form/policy names:

```text
final_score = ExactMatchKeywordWeight * keyword_score
            + (1 - ExactMatchKeywordWeight) * dense_score
```

Return the top configured `Limit` results.

#### Task 3.H4 — Chat pipeline integration

Update `ChatOrchestrator` to call `HybridSearchService` directly.

Keep the existing `ScoredChunk` model so downstream prompt building and citation parsing do not need to change.

#### Task 3.H5 — bge-m3 sparse-vector decision

Decision: do not implement bge-m3 sparse-vector storage in Phase 3.

The current implementation uses TEI `/embed` through `IEmbeddingProvider`, which returns dense float arrays only. Keep the final Phase 3 hybrid retrieval implementation as:
- dense semantic search via existing embeddings
- keyword lexical scoring over Qdrant chunk payloads
- hybrid merge/rerank in `HybridSearchService`

Future optimization:
- If the embedding service exposes sparse lexical vectors later, extend the Qdrant collection schema to store dense and sparse vectors.
- Upsert dense and sparse vectors during ingestion.
- Use Qdrant native hybrid search or query fusion.

#### Task 3.H6 — Hybrid retrieval verification

Verify retrieval with questions that exercise both dense and keyword behavior:

| Case | Query | Expected behavior |
|---|---|---|
| Semantic | "How many annual leave days do employees get with seniority?" | Finds annual-leave policy even if wording differs |
| Policy code | "SCH-HR-003" | Finds the exact annual-leave policy |
| Form name | "Payment Request Form" | Finds the DOCX form template and download path |
| Vietnamese form title | "GIẤY ĐỀ NGHỊ THANH TOÁN" | Finds payment request form/process |
| Acronym | "PCCC equipment" | Finds CII Tower fire safety content |
| Article reference | "Article 4 annual leave" | Finds the annual-leave policy page with Article 4 |

Report comparison:
- Dense-only top results
- Keyword-only top results
- Hybrid top results
- Which strategy produced the best citation/source match

---

## Done criteria

- Language detection returns correct ISO code for EN, VI, FR, DE
- Intent classifier correctly handles smalltalk, policy queries, out-of-scope
- Vector search returns relevant chunks with correct metadata
- LLM responds in the same language as the question
- Citations parse correctly including `image_caption` chunks
- `FormRegistryService` loads `form-registry.json` at startup without errors
- Form download refs appear in SSE sources when answer relates to a form template
- DOCX template downloads correctly via `/templates/` path
- SSE endpoint streams tokens in real time
- All 4 language test cases pass
