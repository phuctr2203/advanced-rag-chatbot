# Phase 5 — Polish & Demo Prep

Goal: make core system stable, verify with real documents, and prepare final demo.

Do not add new features in this phase unless needed to pass acceptance criteria.

## Task 5.1 — Error handling

Handle these without unstructured 500 responses:

- LLM unavailable or timeout.
- TEI unavailable.
- Qdrant unavailable.
- Unsupported file type.
- Corrupt or empty document.
- LibreOffice conversion failure.
- Empty retrieval results.

API response principles:

- Use clear user-facing message.
- Log technical details server-side.
- Do not leak API keys or internal stack traces.

Suggested response shape:

```json
{
  "error": "Unsupported file type. Please upload PDF, DOCX, DOC, XLSX, or PPTX."
}
```

## Task 5.2 — Multilingual test

For EN, VI, FR, DE verify:

- Language detection result.
- Intent classification.
- LLM answer language.
- Citation format.
- Frontend rendering of diacritics and special characters.

Use same policy topic across all languages to compare source consistency.

## Task 5.3 — Citation accuracy

Procedure:

1. Upload all available company policy files.
2. Ask at least 5 questions for each domain:
   - HR
   - General/IT/finance/operations
   - CII Tower support
3. Manually open cited file/page.
4. Confirm cited page contains answer.
5. Record failures and fix parsing/chunking/prompt issues.

Pass criteria:

- Every demo question cites correct file and page.
- No answer relies on outside knowledge.

## Task 5.4 — Demo script

Prepare flow:

1. Start Docker services.
2. Start API.
3. Start frontend.
4. Upload one PDF with auto-classification.
5. Upload one XLSX form with manual agent selection.
6. Ask English policy question.
7. Ask Vietnamese equivalent.
8. Ask CII Tower facilities question.
9. Ask unrelated question; show refusal.
10. Say greeting; show smalltalk.
11. Show citation chip and image.

Prepare backup:

- Screenshots or recorded output for TEI startup delay.
- Known working sample questions.
- Known source file/page mapping.

## Task 5.5 — README

README must cover:

- Prerequisites:
  - Docker Desktop
  - .NET 10 SDK
  - Node.js
  - LibreOffice for DOC/PPTX conversion
- Start infrastructure:
  - `docker compose up -d`
- Create Qdrant collection.
- Run API.
- Run frontend.
- Ingest documents through UI and curl.
- Switch LLM provider through `appsettings.json`.
- Troubleshooting:
  - TEI first-run model download
  - Qdrant collection missing
  - LibreOffice not found
  - LLM API key missing

## Done criteria

- Real documents tested.
- Demo script rehearsed.
- README works from clean setup.
- Known demo questions pass consistently.
