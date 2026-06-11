# Enhancement — Dense Retrieval Reranking

> **When to implement:** After the RAGAS evaluation harness is working and a local reranker endpoint has passed the multilingual smoke test.
> Feed this file to Codex when starting the reranking implementation.

---

## Problem

The RAGAS pilot shows this retrieval pattern:

```text
Context recall:       high
Context precision:    lower
File Hit@1:           low
Page Hit@1:           very low
```

This means the retriever often finds useful information somewhere in the candidate set, but the most useful chunk is not consistently ranked first. The generator then receives noisy context, which lowers answer correctness and citation quality.

The current project also has inconsistent retrieval paths:

```text
Evaluation endpoint -> HybridSearchService
Streaming chat      -> VectorStoreService direct dense search
```

For the next iteration, use a simpler and more measurable pipeline:

```text
Dense vector search -> multilingual reranker -> final top K -> prompt
```

Skip keyword/hybrid search for now so reranker impact can be measured cleanly.

---

## Chosen reranker

Use the local HTTP reranker service already tested:

```text
Model:    jinaai/jina-reranker-v2-base-multilingual
Endpoint: http://127.0.0.1:8081/rerank
```

Service files live under:

```text
python_service/
  reranker_service.py
  requirements.txt
```

Smoke-test result:

```text
EN: PASS
VI: PASS
FR: PASS
DE: PASS
```

The service contract is:

```http
POST /rerank
```

Request:

```json
{
  "model": "jinaai/jina-reranker-v2-base-multilingual",
  "query": "How many annual leave days are added by seniority?",
  "documents": ["chunk 1 text", "chunk 2 text"],
  "top_n": 6,
  "return_documents": false
}
```

Response:

```json
{
  "model": "jinaai/jina-reranker-v2-base-multilingual",
  "results": [
    { "index": 0, "relevance_score": 0.91, "score": 0.91 },
    { "index": 1, "relevance_score": 0.12, "score": 0.12 }
  ]
}
```

---

## Target pipeline

```text
User question
  -> LanguageDetectionService
  -> IntentClassifierService
  -> Dense vector search top N candidates
  -> Reranker scores query/chunk pairs
  -> Keep final top K chunks
  -> PromptBuilderService
  -> LlmService
  -> SourceCitationParser
```

Recommended defaults:

```text
Dense candidate limit: 30
Final context limit:   6
Reranker timeout:      30 seconds
Reranker enabled:      true
Fallback behavior:     use dense ranking if reranker is unavailable
```

---

## Configuration

Add options:

```json
{
  "Retrieval": {
    "Mode": "DenseRerank",
    "FinalLimit": 6,
    "CandidateLimit": 30,
    "MinimumScore": 0.45
  },
  "Reranker": {
    "Enabled": true,
    "BaseUrl": "http://127.0.0.1:8081",
    "Endpoint": "/rerank",
    "ApiKey": "",
    "Model": "jinaai/jina-reranker-v2-base-multilingual",
    "TimeoutSeconds": 30,
    "FallbackToDenseOnError": true
  }
}
```

Notes:

- Keep `HybridSearch` config temporarily for rollback and comparison, but do not use it in the active chat/evaluation path.
- `CandidateLimit` must be higher than `FinalLimit`; reranking only the final 6 dense results will not fix ranking misses beyond rank 6.
- `MinimumScore` should apply to dense candidate retrieval before reranking only if it does not reduce recall too much. If recall drops, prefer filtering after rerank.

---

## Implementation design

### 1. Add reranker provider

Create:

```text
src/API/Options/AppOptions.cs
  RerankerOptions
  RetrievalOptions

src/API/Services/Query/IRerankerService.cs
src/API/Services/Query/HttpRerankerService.cs
src/API/Services/Query/DenseRerankSearchService.cs
```

`HttpRerankerService` responsibilities:

- Call `/rerank` with query and candidate chunk texts.
- Parse `results[].index` and `results[].relevance_score`.
- Preserve original `ParsedChunk` metadata.
- Return chunks ordered by reranker score.
- Log and fallback cleanly when the reranker is disabled, unavailable, times out, or returns malformed output.

### 2. Add dense rerank search service

`DenseRerankSearchService` responsibilities:

```text
vectorStoreService.SearchAsync(query, CandidateLimit)
  -> reranker.RerankAsync(query, candidates, FinalLimit)
  -> return final top K ScoredChunk
```

The final `ScoredChunk.Score` should become the reranker score when reranking succeeds. Keep dense score available only if a model/debug DTO is added later.

### 3. Use one retrieval path everywhere

Update both:

```text
ChatOrchestrator
EvaluationChatOrchestrator
```

to call the same retrieval service:

```text
DenseRerankSearchService.SearchAsync(message, topK, ct)
```

This is important. Evaluation must measure the same retrieval behavior used by the user-facing chat endpoint.

### 4. Add verification endpoint

Add or update a diagnostic endpoint:

```http
POST /verify/rerank-search
```

Response should include:

```text
denseCandidates
rerankedResults
rerankerEnabled
rerankerUsed
fallbackReason
timingsMs
```

This makes debugging easier when the final RAGAS numbers change.

### 5. Update evaluation timing

Extend evaluation timings:

```text
Dense retrieval time
Reranking time
Total retrieval time
Generation time
Total time
```

For the Excel report, keep existing `Retrieval Time Ms` as the total retrieval stage, and optionally add `Dense Retrieval Time Ms` and `Reranking Time Ms` later.

---

## Failure handling

Reranker failure must not break chat.

If reranker is unavailable:

```text
log warning
return dense top K
mark fallback in diagnostics/evaluation metadata
```

Cases to handle:

| Case | Behavior |
|---|---|
| `Reranker.Enabled = false` | Skip reranker, use dense top K |
| HTTP timeout | Fallback to dense top K |
| HTTP 500 | Fallback to dense top K |
| Malformed response | Fallback to dense top K |
| Result index out of range | Ignore malformed item; fallback if no valid items remain |
| Reranker returns fewer than K | Use reranked items first, fill from dense candidates |

---

## Evaluation plan

Run the same dataset before and after reranking.

Compare these metrics first:

```text
Context Precision
Context Recall
Context Relevance
Faithfulness
Answer Correctness
File Hit@1
File Hit@3
File Hit@6
Page Hit@1
Page Hit@3
Page Hit@6
Average Retrieval Time Ms
Average Total Time Ms
```

Expected improvements:

```text
Context Precision: up
File Hit@1:        up
Page Hit@1:        up
Answer Correctness: up
```

Expected tradeoff:

```text
Retrieval Time Ms: up
```

Guardrail:

```text
Context Recall should not drop meaningfully.
```

If recall drops, increase `CandidateLimit` from 30 to 50 before changing chunking or prompts.

---

## Files to modify

| File | Change |
|---|---|
| `src/API/Options/AppOptions.cs` | Add `RetrievalOptions` and `RerankerOptions` |
| `src/API/DependencyInjection/OptionsServiceCollectionExtensions.cs` | Register new option sections |
| `src/API/DependencyInjection/QueryServiceCollectionExtensions.cs` | Register reranker and dense-rerank services |
| `src/API/Services/Query/HttpRerankerService.cs` | New HTTP client service for `/rerank` |
| `src/API/Services/Query/DenseRerankSearchService.cs` | New retrieval orchestration service |
| `src/API/Services/Query/ChatOrchestrator.cs` | Use dense-rerank retrieval path |
| `src/API/Services/Query/EvaluationChatOrchestrator.cs` | Use same dense-rerank retrieval path |
| `src/API/Models/EvaluationChatModels.cs` | Optionally expose reranker/fallback metadata |
| `src/API/Controllers/VerificationController.cs` | Add rerank diagnostic endpoint |
| `src/API/appsettings.json` | Add retrieval/reranker config |
| `evaluation/ragas-evaluation-plan.md` | Note reranker comparison workflow |

---

## Checklist

- [ ] Add `RetrievalOptions` and `RerankerOptions`
- [ ] Register retrieval/reranker options in DI
- [ ] Add `IRerankerService`
- [ ] Add `HttpRerankerService` with `/rerank` request/response DTOs
- [ ] Add reranker timeout and API-key handling
- [ ] Add fallback-to-dense behavior for reranker failures
- [ ] Add `DenseRerankSearchService`
- [ ] Update `ChatOrchestrator` to use dense-rerank search
- [ ] Update `EvaluationChatOrchestrator` to use dense-rerank search
- [ ] Add `/verify/rerank-search` diagnostic endpoint
- [ ] Add reranker/fallback/timing metadata to evaluation response if useful
- [ ] Update `appsettings.json` with `Retrieval` and `Reranker` sections
- [ ] Verify `dotnet build src/API/PolicyBot.Api.csproj -c Release`
- [ ] Verify `/verify/rerank-search` with `http://127.0.0.1:8081/rerank`
- [ ] Verify `/api/evaluation/chat` returns reranked contexts
- [ ] Run RAGAS pilot before/after comparison on `test_evaluation_dataset.xlsx`
- [ ] Decide final `CandidateLimit` and `FinalLimit` from metrics

---

## Acceptance criteria

- User-facing chat and evaluation endpoint use the same dense-rerank retrieval path.
- Reranker can be enabled/disabled by config.
- If the reranker service is down, chat still works using dense retrieval.
- Reranked result metadata preserves `source_file`, `page`, `chunk_index`, `chunk_type`, image paths, template paths, and form flags.
- RAGAS pilot shows improved `Context Precision`, `File Hit@1`, or `Page Hit@1` without a meaningful `Context Recall` regression.
- Retrieval latency increase is visible in evaluation timing and accepted or tuned.

---

## Notes

- Do not call the reranker from ingestion. Reranking is query-time only.
- Do not use Ollama chat/completion for reranking. The tested GGUF/Ollama artifact loaded but did not expose usable cross-encoder relevance scores.
- Keep the local reranker service in `python_service/` and outside the .NET API process for now. It can later be moved to Docker Compose once the retrieval gain is proven.
- The first reranker request after service start can be slow because the model loads lazily. For demos or evaluation, start the service and warm it with a small `/rerank` call first.
