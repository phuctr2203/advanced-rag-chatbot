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
- Build `List<SourceRef>` with `File`, `Page`, `ChunkType`, `ImagePath`
- If chunk is `image_caption`, `ImagePath` is populated — frontend will show the image

```csharp
public class SourceRef
{
    public string File { get; set; }
    public int Page { get; set; }
    public string ChunkType { get; set; }   // "text" or "image_caption"
    public string ImagePath { get; set; }   // populated for image_caption chunks
}
```

---

## Task 3.9 — POST /api/chat SSE endpoint

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

Final SSE event sends structured sources JSON:
```
data: [SOURCES]{"sources":[{"file":"leave_policy.pdf","page":3,"chunkType":"text","imagePath":""}]}
```

Frontend listens for the `[SOURCES]` prefix to separate the answer stream from citations.

---

## Task 3.10 — End-to-end verification

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

Also test intent handling:
- "Hi" → `SMALLTALK` response, no Qdrant call
- "What is the capital of France?" → `OUT_OF_SCOPE` response
- Question with no matching docs → `NoResults` response

---

## Done criteria

- Language detection returns correct ISO code for EN, VI, FR, DE
- Intent classifier correctly handles smalltalk, policy queries, out-of-scope
- Vector search returns relevant chunks with correct metadata
- LLM responds in the same language as the question
- Citations parse correctly including image_caption chunks
- SSE endpoint streams tokens in real time
- All 4 language test cases pass