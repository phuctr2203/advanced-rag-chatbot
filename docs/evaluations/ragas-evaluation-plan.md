# RAGAS Evaluation Plan

Goal: build a repeatable offline evaluation harness for the policy bot using the complete
44-file corpus in `data/test`, approximately 100 reviewed test questions, RAGAS quality
metrics, system-specific checks, and a DOCX report suitable for the demo.

The evaluation runner is separate from the frontend. It must exercise the real retrieval
and answer-generation pipeline, preserve retrieved context metadata, and avoid modifying
Qdrant unless an explicit ingestion command is used.

## Evaluator profiles

Run the same dataset with both evaluator profiles:

| Profile | Provider | Model |
|---|---|---|
| `ollama-gpt-oss-120b` | Ollama | `gpt-oss:120b-cloud` |
| `openwebui-gpt-oss-120b` | OpenWebUI | `inference-gpt-oss-120b` |

The chatbot answer model remains the configured application model. Evaluator models judge
the generated answer and retrieved contexts; they do not replace the chatbot model.

## Corpus inventory

The current `data/test` corpus contains 44 files:

| File type | Count |
|---|---:|
| PDF | 29 |
| DOCX | 6 |
| XLSX | 5 |
| DOC | 3 |
| PPTX | 1 |
| Total | 44 |

The corpus includes policies, guidance documents, forms, bilingual documents, newer
revisions, older revisions, and likely duplicate representations. The audit must identify
canonical sources and version relationships before question generation.

## Question dataset target

Generate approximately 100 reviewed questions. Every row must have an explicit purpose and
must remain traceable to the corpus.

### Scenario distribution

| Scenario | Count | Notes |
|---|---:|---|
| Grounded text policy questions | 72 | Direct facts, numeric facts, eligibility rules, procedures, and multi-step answers |
| Form and template surfacing | 12 | Verify a relevant downloadable form appears only when justified |
| Image surfacing | 4 | Verify image-caption retrieval and UI image references, including CII emergency flowcharts |
| Smalltalk controls | 4 | One greeting for each supported language; no vector search or sources |
| Out-of-scope controls | 4 | Polite refusal; no vector search or sources |
| Mixed greeting and policy query | 4 | Must classify as `POLICY_QUERY`, not `SMALLTALK` |
| Total | 100 | |

### Language distribution

| Language | Text policy | Form | Image | Smalltalk | Out of scope | Mixed | Total |
|---|---:|---:|---:|---:|---:|---:|---:|
| English | 36 | 6 | 2 | 1 | 1 | 1 | 47 |
| Vietnamese | 18 | 4 | 2 | 1 | 1 | 1 | 27 |
| French | 9 | 1 | 0 | 1 | 1 | 1 | 13 |
| German | 9 | 1 | 0 | 1 | 1 | 1 | 13 |
| Total | 72 | 12 | 4 | 4 | 4 | 4 | 100 |

French and German questions may test multilingual retrieval against English or bilingual
source documents. Their reference answers must remain grounded in the same corpus.

### Domain coverage

Policy-query rows must cover all three routing domains:

| Domain | Minimum policy-query rows |
|---|---:|
| `ELCA_HR` | 32 |
| `ELCA_GENERAL` | 28 |
| `CII_TOWER_SUPPORT` | 20 |
| Cross-domain or version-aware | 8 |

Rows may carry more than one domain tag when a question intentionally tests ambiguity or
version handling.

### Question-type tags

Use tags so results can be sliced by behavior:

- `direct_fact`
- `numeric_fact`
- `procedure`
- `multi_step`
- `eligibility`
- `prohibition`
- `comparison`
- `version_aware`
- `multilingual_parity`
- `form_download`
- `image_surface`
- `smalltalk`
- `out_of_scope`
- `mixed_intent`
- `no_answer`

## Dataset schema

Store the reviewed dataset in `evaluation/datasets/rag-evaluation-v1.json`.

```json
[
  {
    "id": "hr-leave-en-001",
    "domain": "ELCA_HR",
    "language": "en",
    "questionType": "direct_fact",
    "question": "What is the annual leave entitlement?",
    "referenceAnswer": "A reviewed answer grounded in the source document.",
    "expectedIntent": "POLICY_QUERY",
    "expectedSources": [
      {
        "file": "source-file.pdf",
        "page": 2
      }
    ],
    "referenceContexts": [
      "The reviewed source excerpt used to validate the answer."
    ],
    "expectedFormDownload": null,
    "expectedImage": false,
    "tags": [
      "core_policy"
    ],
    "reviewStatus": "reviewed"
  }
]
```

For XLSX documents, `page` means worksheet position because the ingestion parser maps each
worksheet to a page number. For converted DOC and PPTX documents, cite the converted
document page produced by the existing ingestion pipeline.

## RAGAS metrics

| Metric | Target | Required data |
|---|---:|---|
| Faithfulness | `>= 0.80` | answer and retrieved contexts |
| Answer relevancy | `>= 0.75` | question and answer |
| Context precision | `>= 0.70` | question, retrieved contexts, and reference answer |
| Context recall | `>= 0.70` | retrieved contexts and reference answer |
| Answer correctness | `>= 0.70` | answer and reference answer |

Use RAGAS `SingleTurnSample` records with:

- `user_input`
- `retrieved_contexts`
- `response`
- `reference`
- `reference_contexts`

## System-specific checks

RAGAS scores do not replace deterministic product checks.

| Check | Pass condition |
|---|---|
| Intent classification | Actual intent equals `expectedIntent` |
| Answer language | Answer language matches dataset language |
| Citation filename | At least one expected filename appears for grounded policy answers |
| Citation page | Cited page is within `+/- 1` of an expected page |
| No misleading citation | No sources for smalltalk, out-of-scope, or no-answer rows |
| Form download | Expected form download appears only for relevant rows |
| Image surfacing | Expected image reference appears for image rows |
| Retrieval bypass | Smalltalk and out-of-scope rows do not run vector search |

## Output artifacts

Each run writes a timestamped directory:

```text
evaluation/results/2026-06-02T120000-baseline/
  run-metadata.json
  raw-results.json
  scores.csv
  summary.json
  rag-evaluation-report.docx
```

The DOCX report contains:

1. Run metadata: timestamp, git commit, indexed document count, chunking strategy, chatbot
   model, evaluator profile, and corpus audit status.
2. Executive summary table with metric targets, scores, and pass/fail.
3. Evaluator comparison table for Ollama and OpenWebUI.
4. Metric breakdown by domain, language, and question type.
5. Per-question result table.
6. Citation, intent, multilingual, image, and form-download check tables.
7. Failed cases with retrieved sources and recommendations.

## Checkpoints

### EVAL 1 - Corpus audit

- Extract readable text from all 44 files using the same parser behavior as ingestion.
- Convert legacy DOC and PPTX files through LibreOffice before extraction.
- Record extraction status, page or sheet count, language, domain, document role, and
  canonical version group.
- Flag empty, corrupt, duplicate, superseded, and image-heavy documents.
- Write `evaluation/corpus/corpus-inventory.json`.
- Write `evaluation/corpus/corpus-audit.md`.

Checkpoint: stop for review before generating questions.

### EVAL 2 - Question draft generation

- Generate grounded draft questions from audited source excerpts.
- Create approximately 100 rows using the target scenario matrix.
- Ensure every policy row contains a reference answer, expected source, page or sheet, and
  reference excerpt.
- Prefer current canonical policies for factual answers.
- Add deliberate version-aware questions only when multiple revisions are useful to test.
- Write `evaluation/datasets/rag-evaluation-v1.draft.json`.

Checkpoint: stop for review before accepting the dataset.

### EVAL 3 - Dataset validation

- Review all draft rows against source excerpts.
- Reject ambiguous questions and unsupported reference answers.
- Detect duplicate questions and overrepresented topics.
- Verify all expected source files exist in the audited corpus.
- Mark accepted rows as `reviewed`.
- Write `evaluation/datasets/rag-evaluation-v1.json`.

Checkpoint: dataset must contain approximately 100 reviewed rows.

### EVAL 4 - Evaluation query endpoint

- Add an evaluation-only API endpoint behind `Evaluation:Enabled`.
- Reuse the real language detection, intent classification, retrieval, prompt, answer, and
  citation services.
- Return answer text, retrieved chunk text, score, filename, page, chunk type, intent,
  language, sources, form downloads, and whether retrieval ran.
- Do not expose this endpoint when evaluation mode is disabled.

Checkpoint: one manually verified evaluation query returns full trace metadata.

### EVAL 5 - Offline runner

- Add a Python runner under `evaluation/`.
- Preflight Qdrant and fail with a missing-file list unless all expected corpus files are
  indexed.
- Query the evaluation endpoint for every dataset row.
- Run RAGAS metrics with both evaluator profiles.
- Use the existing TEI endpoint through a small embeddings adapter where metrics require
  embeddings.
- Add retry, timeout, resumable result writing, and a configurable low concurrency limit.
- Never delete or re-ingest Qdrant data automatically.

Checkpoint: run a 10-question smoke subset before the full dataset.

### EVAL 6 - Report generation

- Generate JSON, CSV, summary JSON, and DOCX output.
- Include RAGAS metric averages and deterministic product-check results.
- Include failures with source metadata for prompt or chunking follow-up.

Checkpoint: review the baseline DOCX before tuning the RAG pipeline.

### EVAL 7 - Baseline and tuning loop

- Run the full 100-question baseline against the indexed 44-file corpus.
- Preserve the baseline artifacts before making changes.
- Re-run after prompt, threshold, chunking, or routing changes.
- Compare runs by metric, domain, language, and question type.

Checkpoint: select demo metrics and known-good demo questions from recorded results.

## Non-goals

- No evaluation controls in the frontend.
- No provider switching in the evaluation runner.
- No automatic rewriting of the reviewed dataset.
- No automatic Qdrant reset, deletion, or re-ingestion.
- No use of the evaluator model as a substitute for source review.

