# Phase 3 — RAG Query Pipeline

Goal: answer company policy questions with grounded context, matching user language, streaming response, and source citations.

Agent-domain routing is not required in Phase 3. Search all documents first. Add agent filter in Phase 6.

## Task 3.1 — Language detection

Implement `LanguageDetectionService`.

Requirements:

- Use NTextCat.
- Return ISO code: `en`, `vi`, `fr`, `de`.
- Default to `en` when unsupported or uncertain.
- Pass `CancellationToken` only where async calls exist; local detection can be sync.

## Task 3.2 — Intent classifier

Implement `IntentClassifierService` with hybrid fast path and LLM fallback.

Intents:

- `SMALLTALK`
- `POLICY_QUERY`
- `OUT_OF_SCOPE`

Fast path:

- Greetings/thanks in all 4 languages → `SMALLTALK`.
- Message length < 10 characters → `SMALLTALK`.
- Policy keywords → `POLICY_QUERY`.

LLM fallback prompt:

```text
Classify the user message into exactly one category:
- SMALLTALK: greetings, thanks, casual conversation
- POLICY_QUERY: any question about company policies, HR, leave, overtime, salary, benefits, IT, facilities, procedures, forms, CII Tower
- OUT_OF_SCOPE: questions unrelated to company policies

When in doubt between POLICY_QUERY and OUT_OF_SCOPE, choose POLICY_QUERY.
Respond with ONLY the category name.

User message: {message}
```

Rules:

- `maxTokens: 10`
- Unexpected response defaults to `POLICY_QUERY`.

## Task 3.3 — Intent responses

Create static `IntentResponses` class.

Provide all 4 languages for:

- `Smalltalk`
- `OutOfScope`
- `NoResults`

Do not ask LLM for these responses.

## Task 3.4 — Vector search

Flow:

1. Embed user query.
2. Search Qdrant across all chunks.
3. Return top 6.
4. Filter score below `0.45f`.
5. If no results, stream `NoResults` response.

Do not apply agent filter yet.

## Task 3.5 — Prompt builder

Implement `PromptBuilderService`.

Template:

```text
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

Context format per chunk:

```text
[Source: {SourceFile}, page {PageNumber}, chunk {ChunkIndex}]
{Text}
```

## Task 3.6 — LLM streaming

Use active `ILlmProvider.StreamAsync`.

Requirements:

- Return `IAsyncEnumerable<string>`.
- Pass `CancellationToken`.
- Work for OpenWebUI and Ollama OpenAI-compatible APIs.
- Preserve token order.

## Task 3.7 — Source citation parser

Parse final `SOURCES:` line into `List<SourceRef>`.

Rules:

- Extract filename and page.
- Match parsed source to retrieved chunks.
- Include image paths from matching chunks.
- If duplicate source/page appears, return once.

## Task 3.8 — Chat SSE endpoint

Endpoint:

```text
POST /api/chat
Content-Type: application/json
Accept: text/event-stream
```

Request:

```json
{
  "message": "What is the annual leave policy?"
}
```

SSE behavior:

- Stream tokens as `data: {token}\n\n`.
- Final event sends structured sources JSON.
- Set headers:
  - `Content-Type: text/event-stream`
  - `Cache-Control: no-cache`

## Task 3.9 — End-to-end verification

Test matrix:

| Language | Question |
|---|---|
| English | What is the annual leave policy? |
| Vietnamese | Chính sách nghỉ phép hàng năm là gì? |
| French | Quelle est la politique de congé annuel? |
| German | Was ist die Richtlinie für den Jahresurlaub? |

Verify:

- Response language matches question.
- Answer uses only context.
- Citation file/page is correct.
- Images display when source page has images.

## Done criteria

- Smalltalk returns static response without Qdrant call.
- Out-of-scope returns static refusal without Qdrant call.
- Policy query searches Qdrant and streams LLM answer.
- Final sources event includes file, page, and image paths.
- All 4 languages pass manual test.
