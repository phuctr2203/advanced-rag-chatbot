# RAGAS Evaluation Phase Plan

## Goal

Evaluate the RAG system end to end with a prepared question-answer dataset and RAGAS metrics. The evaluation should measure both retrieval quality and generated answer quality, while preserving enough row-level evidence to diagnose failures by language, source file, page, and paraphrase group.

The input dataset is expected at:

```text
evaluation/dataset/test_evaluation_dataset.xlsx
```

Required dataset columns:

```text
Language
Question
Answer
Reference Context
Page
File Name
Original Language
Original Question
Original Answer
Paraphrase Number
```

## Evaluation Model

Use `gpt-oss-120b` as the RAGAS evaluator model. It may be served through either Ollama or OpenWebUI.

Recommended config shape:

```json
{
  "evaluator": {
    "provider": "Ollama",
    "baseUrl": "http://localhost:11434/v1",
    "apiKey": "ollama",
    "model": "gpt-oss:120b-cloud"
  }
}
```

OpenWebUI variant:

```json
{
  "evaluator": {
    "provider": "OpenWebUI",
    "baseUrl": "https://your-company-openwebui.com/api",
    "apiKey": "your-key",
    "model": "inference-gpt-oss-120b"
  }
}
```

## API Requirement

Add a non-streaming evaluation endpoint that returns the generated answer and the exact chunks used for generation.

Endpoint:

```http
POST /api/evaluation/chat
```

Request:

```json
{
  "message": "What should employees do if security mechanisms are not operational?",
  "includePrompt": false,
  "topK": 6
}
```

Response:

```json
{
  "question": "What should employees do if security mechanisms are not operational?",
  "answer": "Employees must notify the Service Desk without any delay.",
  "language": "en",
  "intent": "PolicyQuery",
  "retrievedContexts": [
    {
      "rank": 1,
      "text": "If existing security mechanisms such as security patching or protection against malware are not operational, employees must notify the Service Desk without any delay.",
      "score": 0.92,
      "sourceFile": "AcceptableUseOfAssetsVN_EN.pdf",
      "page": 8,
      "chunkIndex": 3,
      "chunkType": "text"
    }
  ],
  "sources": [],
  "timingsMs": {
    "retrieval": 120,
    "generation": 3400,
    "total": 3600
  }
}
```

The endpoint must reuse the real application pipeline:

```text
Question
  -> LanguageDetectionService
  -> IntentClassifierService
  -> HybridSearchService
  -> PromptBuilderService
  -> LlmService
  -> SourceCitationParser
  -> answer + retrieved chunks
```

Do not evaluate through a shortcut retriever or a different prompt path.

## Dataset Mapping

For each spreadsheet row, map fields into the RAGAS sample as follows:

| RAGAS field | Dataset/API source |
|---|---|
| `user_input` | `Question` |
| `response` | `/api/evaluation/chat` response `answer` |
| `retrieved_contexts` | `/api/evaluation/chat` response `retrievedContexts[].text` |
| `reference` | `Answer` |
| `reference_contexts` | `[Reference Context]` |
| metadata | `Language`, `Page`, `File Name`, `Original Question`, `Paraphrase Number` |

Also compute deterministic retrieval diagnostics:

| Diagnostic | Logic |
|---|---|
| `file_hit` | Any retrieved `sourceFile == File Name` |
| `page_hit` | Any retrieved `sourceFile == File Name` and `page == Page` |
| `expected_rank` | First rank where file and page match, otherwise empty |
| `file_hit_at_1/3/6` | Expected file appears within top K |
| `page_hit_at_1/3/6` | Expected file and page appears within top K |

These deterministic checks should be kept even when RAGAS scores are available because they are easier to debug against the policy corpus.

## Metrics

Retriever metrics:

- Context precision
- Context recall
- Context relevance

Generator metrics:

- Faithfulness
- Answer relevancy / response relevancy
- Answer correctness

Pin the installed RAGAS version before implementing the metric imports because names and APIs differ across versions. If `AnswerCorrectness` is unavailable in the installed version, map this requirement to the closest supported correctness metric and document the mapping in the run output.

## Runner Design

Add scripts under `evaluation/`:

```text
evaluation/
  config.example.json
  run_evaluation.py
  run_rag_batch.py
  run_ragas_eval.py
  summarize_results.py
  test_reranker_multilingual.py
  outputs/
```

Recommended two-step workflow:

```text
1. run_rag_batch.py
   - Read XLSX dataset.
   - Call /api/evaluation/chat for each question.
   - Save raw generated rows to JSONL.
   - Support resume/caching by row id.

2. run_ragas_eval.py
   - Read the generated JSONL.
   - Run RAGAS metrics.
   - Save scored rows to CSV/XLSX/JSONL.
```

Keep generation separate from scoring. With 1000+ questions, this avoids rerunning successful chat calls when RAGAS scoring fails or when evaluator settings change.

Convenience workflow:

```powershell
py -3.12 evaluation\run_evaluation.py `
  evaluation\dataset\test_evaluation_dataset.xlsx `
  evaluation\outputs\test-evaluation-dataset\evaluation_result.xlsx
```

The convenience script still writes the intermediate `generated.jsonl`, `ragas_scores.jsonl`, `ragas_scores.csv`, `summary.json`, and `summary.md` files into the run directory.

## Reranker Multilingual Check

Before wiring a hosted `bge-reranker` into the RAG pipeline, verify that it handles the project languages consistently.

Configure:

```json
{
  "reranker": {
    "baseUrl": "https://your-company-reranker-host",
    "endpoint": "/rerank",
    "apiKey": "",
    "model": "bge-reranker",
    "mode": "rerank",
    "timeoutSeconds": 60
  }
}
```

Run:

```powershell
py -3.12 evaluation\test_reranker_multilingual.py --config evaluation\config.example.json
```

If the reranker is only exposed through an OpenAI-compatible chat-completions route, use:

```powershell
py -3.12 evaluation\test_reranker_multilingual.py `
  --config evaluation\config.example.json `
  --base-url https://ai.svc.elca.ch `
  --endpoint /api/v1/chat/completions `
  --mode chat-completions
```

The test sends equivalent EN, VI, FR, and DE questions against the same candidate chunks. The expected result is that the annual-leave chunk ranks first for all four languages. If only English passes, use a stronger multilingual reranker such as a BGE M3-family reranker before changing the production retrieval pipeline.

## Output Files

Per-run output folder:

```text
evaluation/outputs/{yyyyMMdd-HHmmss}/
  generated.jsonl
  ragas_scores.jsonl
  ragas_scores.csv
  evaluation_result.xlsx
  summary.json
  summary.md
```

Per-row output should include:

```text
row_id
language
question
generated_answer
reference_answer
reference_context
retrieved_contexts
retrieved_sources
context_precision
context_recall
context_relevance
faithfulness
answer_relevancy
answer_correctness
expected_file
expected_page
file_hit
page_hit
expected_rank
original_question
paraphrase_number
error
```

Aggregate summary should include:

- Overall average per metric
- Average per language
- Average per source file
- Original-question vs paraphrase performance
- File hit@1, hit@3, hit@6
- Page hit@1, hit@3, hit@6
- Worst 50 questions by combined score
- Retrieval succeeded but generation failed
- Retrieval failed but generation was still correct

## Excel Result Workbook

Generate a final Excel workbook for each evaluation run:

```text
evaluation/outputs/{yyyyMMdd-HHmmss}/evaluation_result.xlsx
```

The workbook should contain at least two sheets.

### Sheet 1: `Question Results`

One row per evaluated question or paraphrase.

Required columns:

```text
Row ID
Language
Question
Generated Answer
Reference Answer
Reference Context
Expected File
Expected Page
Retrieved Files
Retrieved Pages
Top Retrieved Context
Intent
Detected Language
Retrieval Time Ms
Generation Time Ms
Total Time Ms
Context Precision
Context Recall
Context Relevance
Faithfulness
Answer Relevancy
Answer Correctness
File Hit
Page Hit
Expected Rank
Paraphrase Number
Original Question
Error
```

This sheet is used for debugging individual failures. It should support filtering by language, source file, intent, score, hit/miss status, and timing.

### Sheet 2: `Dataset Summary`

Aggregate the full evaluation run.

Required summary fields:

```text
Total Questions
Succeeded
Failed
Average Retrieval Time Ms
Average Generation Time Ms
Average Total Time Ms
P50 Total Time Ms
P95 Total Time Ms
Average Context Precision
Average Context Recall
Average Context Relevance
Average Faithfulness
Average Answer Relevancy
Average Answer Correctness
File Hit@1
File Hit@3
File Hit@6
Page Hit@1
Page Hit@3
Page Hit@6
```

Also include compact breakdown tables on the same sheet:

- Scores by language
- Scores by source file
- Scores by original/paraphrase grouping
- Slowest 20 questions
- Worst 20 questions by average score

Keep the first version to these two sheets. Additional sheets such as `By Language`, `By File`, `Worst Cases`, or `Run Config` can be added later if the summary sheet becomes too dense.

## Execution Plan

1. Implement `/api/evaluation/chat`.
2. Verify the endpoint manually with one known question.
3. Implement `run_rag_batch.py` with JSONL resume support.
4. Run the batch script on `test_evaluation_dataset.xlsx`.
5. Implement `run_ragas_eval.py` using `gpt-oss-120b`.
6. Run RAGAS on the small test dataset.
7. Implement summary generation.
8. Generate `evaluation_result.xlsx` with `Question Results` and `Dataset Summary` sheets.
9. Run a 50-row pilot from the full dataset.
10. Run the full 1000+ question dataset.
11. Use results to decide whether to tune chunking, hybrid weights, prompt, or add a reranker.

## Acceptance Criteria

- Evaluation endpoint returns answer and retrieved chunks from the real RAG pipeline.
- Batch runner can resume without repeating completed rows.
- RAGAS runner produces all requested metric columns or documents any metric-name fallback.
- Summary report identifies retrieval and generation failure modes separately.
- `evaluation_result.xlsx` contains row-level question scores, timings, intent, and full-dataset summary metrics.
- Full dataset run completes and writes row-level plus aggregate outputs.
