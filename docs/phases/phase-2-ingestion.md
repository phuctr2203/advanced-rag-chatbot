# Phase 2 — Document Ingestion Pipeline

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: parse all supported document types, filter and caption images via vision model, chunk text, classify agent domain, embed chunks, and store in Qdrant with full metadata.

> **Prerequisite:** Task 1.7 (vision provider verified) must be complete before starting 2.2.

---

## Supported file types

| Format | Parser | Pre-processing |
|---|---|---|
| `.pdf` | PdfPig | none |
| `.pptx` | PdfPig | LibreOffice → PDF first |
| `.docx` | OpenXml | none |
| `.doc` | OpenXml | LibreOffice → DOCX first |
| `.xlsx` | ClosedXML | none |

---

## Data rules

Every chunk must carry these fields through the full pipeline:

| Field | Type | Notes |
|---|---|---|
| `Text` | string | extracted text or image caption |
| `SourceFile` | string | original filename |
| `PageNumber` | int | page or slide number |
| `ChunkIndex` | int | position within page |
| `ChunkType` | string | `"text"` or `"image_caption"` |
| `FileType` | string | `"pdf"`, `"docx"`, `"xlsx"` |
| `Agent` | string | `"ELCA_HR"`, `"ELCA_GENERAL"`, `"CII_TOWER_SUPPORT"` |
| `ImagePath` | string | URL path for image chunks, empty for text chunks |

---

## Task 2.1 — PDF parser (text)

Implement `PdfParserService` using `UglyToad.PdfPig`.

Behavior:
- Read text per page using `page.GetWords()`
- Join words with spaces to form page text
- Create one `ParsedChunk` per page with `ChunkType = "text"`
- Page number from `page.Number`

Verification: parse a text-based PDF, confirm non-empty text per page.

---

## Task 2.2 — ImageCaptioningService (multi-layer filter + caption)

Implement `Services/Ingestion/ImageCaptioningService.cs` used by both PDF and DOCX parsers.

### Return type

```csharp
public class ImageCaptionResult
{
    public string Caption { get; set; }
}

// Returns null if image should be skipped at any layer
public async Task<ImageCaptionResult?> ProcessImageAsync(
    byte[] imageBytes,
    int width,
    int height,
    string sourceFile,
    int page,
    string pageText,
    CancellationToken ct = default)
```

### Filtering pipeline — run in order, skip image if any layer rejects

**Layer 1 — Size filter**
```csharp
if (width < 100 || height < 100) return null; // too small — icon, bullet, decoration
```

**Layer 2 — Aspect ratio filter**
```csharp
var ratio = (float)width / height;
if (ratio > 5.0f || ratio < 0.2f) return null; // banner, divider, thin line
```

**Layer 3 — Vision classification**

Call vision model with `maxTokens: 5`:

```
Is this image meaningful policy content such as an org chart, process diagram,
form layout, table, or instructional graphic?
Or is it decorative such as a logo, banner, background, divider, or icon?
Reply with ONLY one word: CONTENT or DECORATIVE.
```

- `DECORATIVE` → return `null`
- `CONTENT` → proceed to caption
- Call fails → log and return `null` (never crash ingestion)

**Layer 4 — Caption generation**

Only reached if all filters pass:

```
This image appears in a company policy document called "{sourceFile}", page {page}.
The surrounding text on this page discusses: "{first150charsOfPageText}".
Describe what this image shows in 2-3 sentences. Focus on content relevant to company policies.
```

- Empty or whitespace caption → return `null`
- Return caption string

### Usage in parsers

```csharp
var result = await _captioningService.ProcessImageAsync(
    imageBytes, width, height, sourceFile, pageNumber, pageText, ct);

if (result is null) continue; // filtered out

// Save to disk
var relativePath = $"/images/{docName}/page{pageNumber}_img{i}.png";
Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
File.WriteAllBytes(diskPath, imageBytes);

// Create caption chunk
chunks.Add(new ParsedChunk
{
    Text      = result.Caption,
    ChunkType = "image_caption",
    ImagePath = relativePath,
    SourceFile = sourceFile,
    PageNumber = pageNumber,
    FileType   = fileType
});
```

Verification:
- Logo image → `null` (DECORATIVE)
- Org chart image → non-empty caption (CONTENT)
- 50×50 icon → `null` (Layer 1)
- Wide banner → `null` (Layer 2)

---

## Task 2.3 — PDF parser (image extraction)

Extend `PdfParserService` to process images per page using `ImageCaptioningService`.

For each image found via `page.GetImages()`:

1. Extract `width`, `height`, `RawBytes` — skip if `RawBytes` is null
2. Pass to `ImageCaptioningService.ProcessImageAsync()`
3. If result is `null` → skip
4. If result has caption → save image to disk + create `image_caption` chunk

Rule: always extract page text first so `pageText` context is available for captioning.

Verification: PDF with a logo and an org chart — logo skipped, org chart captioned and stored.

---

## Task 2.4 — PPTX → PDF conversion

Implement `FileConversionService.ToPdfAsync(string filePath)`:

```bash
libreoffice --headless --convert-to pdf --outdir {tempDir} {filePath}
```

- Route output PDF into `PdfParserService`
- Slide number = page number in citations

Verification: convert a PPTX, confirm output PDF page count equals slide count.

---

## Task 2.5 — DOCX parser

Implement `DocxParserService` using `DocumentFormat.OpenXml`.

Behavior:
- Extract non-empty paragraphs from `body.Elements<Paragraph>()`
- Track page breaks via `Break` elements — increment page counter when found
- Estimate page number if no explicit page break
- Extract images from `ImagePart` relationships (document-level)
- Pass each image through `ImageCaptioningService.ProcessImageAsync()` — same filter layers apply
- Skip images that return `null`

Note: DOCX has no per-page image API. Attach caption chunks at document level with estimated page number.

Verification: parse a DOCX with paragraphs and embedded images — logos filtered, diagrams captioned.

---

## Task 2.6 — DOC → DOCX conversion

Implement `FileConversionService.ToDocxAsync(string filePath)`:

```bash
libreoffice --headless --convert-to docx --outdir {tempDir} {filePath}
```

Route output DOCX into `DocxParserService`.

Verification: converted DOCX parses and preserves useful text.

---

## Task 2.7 — XLSX parser

Implement `XlsxParserService` using `ClosedXML`.

Rules:
- Do NOT dump raw cell values
- Treat each sheet as one page
- Reconstruct each row as readable prose:
  ```
  Field: Request Type — Value: Laptop Purchase — Description: Equipment request form
  ```
- Filter empty rows
- No image processing for form templates

Verification: internal purchasing form produces readable policy context sentences.

---

## Enhancement Phase 2 — parser/storage hardening

These enhancements were added while validating tasks 2.1–2.7. They support the parser endpoints before the full ingestion orchestrator is implemented.

### Enhancement 2.E1 — Persistent uploaded document storage

Store original uploaded documents under:

```text
data/uploads/{yyyyMMdd}/{first16Sha256}_{originalFileName}
```

Rules:
- Compute SHA-256 from file content before saving
- Reuse the existing file if the same content and filename are uploaded on the same day
- Return document metadata from parser endpoints: `originalFileName`, `storedFileName`, `url`, `sha256`, `reused`
- Serve stored originals from `/documents`

Purpose: keep original files available for future UI display, download, and citations.

---

### Enhancement 2.E2 — Centralized data path resolution

Resolve configured ingestion paths from the API content root instead of the build output folder.

Storage layout:

```text
data/uploads/     original uploaded documents
data/temp/        LibreOffice and OCR intermediate files
data/images/      extracted meaningful PDF/DOCX images
data/templates/   downloadable DOCX form templates
```

Rules:
- `UploadedDocumentsPath`, `TempPath`, `ImageStorePath`, and `TemplatesStorePath` must resolve consistently
- `/documents`, `/images`, and `/templates` static routes must point to the same resolved folders used by parser/storage services

---

### Enhancement 2.E3 — PDF OCR fallback

Keep PdfPig as the default PDF parser. Before parsing, check extracted text quality:

- Total extracted characters
- Average words per page

If text quality is below configured thresholds, run OCRmyPDF:

```bash
ocrmypdf --skip-text input.pdf output.pdf
```

Then parse the OCR output PDF with PdfPig.

Config:

```json
{
  "Ingestion": {
    "EnablePdfOcrFallback": true,
    "PdfOcrMinimumTextCharacters": 100,
    "PdfOcrMinimumAverageWordsPerPage": 10,
    "OcrMyPdfExecutable": "ocrmypdf"
  }
}
```

Rules:
- If OCRmyPDF is missing or fails, log a warning and continue with the original PDF
- Support full executable path in `OcrMyPdfExecutable`
- Do not crash ingestion when OCR is unavailable

---

### Enhancement 2.E4 — Skip full-page scanned PDF images

OCRmyPDF keeps the original scanned page image and adds a text layer. PdfPig can then see both OCR text and a full-page image.

Skip PDF images before captioning when they cover nearly the whole page:

- Image covers at least 85% of page width
- Image covers at least 85% of page height
- Image aspect ratio is close to the page aspect ratio

Purpose: avoid duplicate `image_caption` chunks that describe the whole scanned page when OCR text is already available.

---

### Enhancement 2.E5 — Broader policy-content image classification

Update the vision classification prompt so workplace safety and facility equipment images count as `CONTENT`, not decorative.

Examples that should classify as `CONTENT`:
- Tools
- Workplace safety equipment
- Facility equipment
- Fire alarm box
- Smoke or heat detector
- Automatic sprinkler head
- Fire hose cabinet
- Portable fire extinguisher

The classifier still replies with only `CONTENT` or `DECORATIVE`.

---

### Enhancement 2.E6 — DOC/DOCX form template storage

After DOCX parsing, classify whether the document is a blank form template or a policy/procedure document.

Inputs:
- Original filename
- First extracted document text

LLM prompt response:

```text
FORM
```

or

```text
POLICY
```

Rules:
- For `.docx`, classify the stored original DOCX
- For `.doc`, convert to DOCX first, then classify the converted DOCX
- If classified as `FORM`, copy the DOCX into `data/templates`
- Store template files using SHA-256 naming: `data/templates/{first16Sha256}_{originalFileName}`
- Return template metadata from DOC/DOCX parser endpoints
- Tag returned chunks with `IsFormTemplate = true` and `TemplatePath = "/templates/..."`
- Serve templates from `/templates`

Purpose: make uploaded form templates downloadable later from the UI and available for registry linking.

---

### Enhancement 2.E7 — RecursiveBoundary chunking strategy

Add optional `RecursiveBoundary` chunking for difficult OCR or poorly structured documents.

Order:

1. Heading/paragraph boundaries
2. Sentence boundaries
3. Word windows
4. Character fallback only for pathological long tokens or unbroken text

Rules:
- Keep `ParagraphBoundary` as the default unless evaluation proves otherwise
- Preserve `image_caption` chunks as-is
- Preserve all `ParsedChunk` metadata
- Use character splitting only as a last resort, not as the primary policy-document strategy

Test endpoint:

```text
POST /api/ingest/chunk/recursive-boundary
```

---

## Tasks 2.8–2.10 — Text chunking strategies

Implement `TextChunkerService` with selectable strategy via `Ingestion:ChunkingStrategy` config.

### Strategy A — FixedSize

- Split by word count: 400 words per chunk, 80-word overlap
- Drop chunks under 30 words

### Strategy B — ParagraphBoundary *(default)*

- Split on blank lines and heading-like patterns first
- Keep paragraphs intact when under 400 words
- Fall back to FixedSize for long paragraphs

### Strategy C — SentenceWindow

- Split on sentence endings: `.`, `?`, `!`, `。`
- Accumulate sentences until 400-word target
- Carry last 1–2 sentences into the next chunk as overlap

All strategies must:
- Skip `image_caption` chunks — they are not re-chunked
- Preserve all `ParsedChunk` metadata fields on output chunks

---

## Task 2.11 — Chunker evaluation

Before ingesting all 20 documents:

1. Ingest 2–3 policy documents with each strategy into separate test collections (`policy_docs_fixed`, `policy_docs_para`, `policy_docs_sentence`)
2. Ask 5 representative questions against each collection
3. Compare coherence, answer completeness, citation precision
4. Choose best strategy and record decision in `.claude/PROGRESS.md` under the chunker note

---

## Task 2.12 — Manual document classifier

Accept optional `agent` query param:

```
POST /api/ingest?agent=ELCA_HR
```

Valid values: `ELCA_HR`, `ELCA_GENERAL`, `CII_TOWER_SUPPORT`

If provided → skip LLM classification, apply agent directly to all chunks.

---

## Task 2.13 — LLM auto-classify fallback

When `agent` param is missing, classify using first 500 words:

```
You are a document classifier for ELCA company.
Classify the document excerpt into exactly one category:
- ELCA_HR: HR policies, leave, recruitment, benefits, employee conduct, salary
- ELCA_GENERAL: Company general policies, IT, security, operations, finance
- CII_TOWER_SUPPORT: CII Tower building, facility, support services, maintenance
Respond with ONLY the category name. Nothing else.

Document excerpt:
{first500Words}
```

Rules:
- `maxTokens: 10`
- One call per document
- Unexpected response defaults to `ELCA_GENERAL`
- Log chosen category

Implementation note:
- `DocumentClassifierService` owns manual validation and LLM fallback classification.
- Parser and conversion test endpoints accept optional `?agent=` now so classification can be verified before the full ingestion orchestrator is added.
- The full `POST /api/ingest` endpoint remains part of tasks 2.14-2.15.

---

## Task 2.14 — Ingestion orchestrator

`DocumentIngestionService` flow:

```
1. Validate file extension
2. Convert if needed (DOC→DOCX, PPTX→PDF)
3. Parse file → text chunks + image caption chunks
   (image filter layers run inside ImageCaptioningService)
4. Determine agent (manual param OR LLM fallback)
5. Apply agent to all chunks
6. Run text chunks through chunker (image_caption chunks skipped)
7. Embed all chunks in batches of 32
8. Upsert to Qdrant with full payload
9. Return { filename, agent, chunkCount }
```

Implementation note:
- `DocumentIngestionService` lives under `Services/Ingestion/Orchestration`.
- The orchestrator accepts a `StoredDocument`, so upload persistence and SHA-256 dedupe remain owned by `UploadedDocumentStorageService`.
- DOC, DOCX, and XLSX template detection/storage are preserved before chunking; template metadata is re-applied after chunking.
- Qdrant payload now includes the enhanced metadata needed by later UI/features: `image_paths`, `is_form_template`, and `template_path`.
- The public `POST /api/ingest` endpoint is wired in task 2.15.

---

## Task 2.15 — Ingest endpoint

```
POST /api/ingest?agent=ELCA_HR     (agent param optional)
Content-Type: multipart/form-data
```

Response:
```json
{
  "message": "leave_policy.pdf ingested successfully",
  "agent": "ELCA_HR",
  "chunks": 42
}
```

- Return `400` for unsupported file types
- Return `400` for invalid agent values

Implementation note:
- The endpoint persists the uploaded original with `UploadedDocumentStorageService` before ingestion.
- The response keeps the required `message`, `agent`, and `chunks` fields and also returns stored `document` metadata plus optional `template` metadata for future UI linking.

---

## Task 2.16 — Static image serving

`Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});
```

Verification: upload a PDF with images, access `/images/{docName}/page1_img0.png` in browser, confirm it loads.

---

## Task 2.17 — Form registry setup

Create `data/form-registry.json` at project root. This file is maintained manually — you link PDF form mentions to their downloadable DOCX files here.

### Schema

```json
[
  {
    "form_name": "Payment Request Form",
    "aliases": [
      "payment request form",
      "request for payment",
      "đề nghị thanh toán",
      "giấy đề nghị thanh toán"
    ],
    "docx_file": "Payment_request_form.docx",
    "download_path": "/templates/Payment_request_form.docx",
    "agent": "ELCA_GENERAL"
  }
]
```

| Field | Description |
|---|---|
| `form_name` | Display name shown in the chat UI download button |
| `aliases` | All name variations the LLM might extract — used for matching |
| `docx_file` | Actual filename of the DOCX template |
| `download_path` | URL path served by the static file server |
| `agent` | Which agent domain this form belongs to |

### How to populate it

**Step 1 — ingest the PDF** that references forms. The LLM auto-classifier (Task 2.13) will read the document. Separately, after ingestion, run the form extractor (Task 2.18) to generate a draft registry with `aliases` filled in and `docx_file: null`.

**Step 2 — ingest the DOCX form templates.** The DOCX parser will detect them as form templates (Task 2.19).

**Step 3 — manually edit** `form-registry.json` to fill in `docx_file` and `download_path` for each entry. This is a one-time setup per document batch.

---

## Task 2.18 — LLM form mention extractor

When ingesting a PDF, scan for form mentions and generate draft registry entries automatically.

Run after parsing, before chunking. Send the full document text to the LLM:

```
You are analyzing a company policy document.
Extract all form names or template names mentioned in this document.
For each form found, provide:
- The official form name in English
- All aliases or alternative names used (in any language found in the document)

Respond in JSON only. No explanation. Format:
[
  {
    "form_name": "Payment Request Form",
    "aliases": ["payment request", "đề nghị thanh toán", "giấy đề nghị thanh toán"]
  }
]

Document text:
{fullDocumentText}
```

Rules:
- `maxTokens: 500`
- Strip markdown fences before parsing JSON
- If parsing fails, log and skip — do not crash ingestion
- Write draft entries to `data/form-registry-draft.json` with `docx_file: null`
- Never overwrite existing `form-registry.json` automatically — drafts only

After running, log a message:
```
[FormExtractor] Found 3 form mentions in Payment_process_V2_2023.pdf.
Draft written to data/form-registry-draft.json — review and link docx_file manually.
```

---

## Task 2.19 — DOCX form template detection

When ingesting a DOCX file, detect whether it is a form template (vs a policy document).

Send the first 300 words to the LLM:

```
Is this document a blank form template that employees fill in,
or is it a policy/procedure document?
Reply with ONLY one word: FORM or POLICY
```

Rules:
- `maxTokens: 5`
- If `FORM` → tag all chunks with `is_form_template: true` in Qdrant payload
- If `POLICY` → proceed normally, `is_form_template: false`
- Log result: `[FormDetector] Payment_request_form.docx detected as: FORM`

Updated Qdrant payload for form template chunks:

```json
{
  "source_file": "Payment_request_form.docx",
  "page": 1,
  "chunk_index": 0,
  "chunk_type": "text",
  "file_type": "docx",
  "agent": "ELCA_GENERAL",
  "image_path": "",
  "is_form_template": true
}
```

---

## Task 2.20 — Static template file serving

Serve DOCX form templates as downloadable files.

Store all form DOCX files in `data/templates/` and serve via static file middleware:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(templatesStorePath),
    RequestPath = "/templates"
});
```

Add to `appsettings.json`:
```json
{
  "Ingestion": {
    "TemplatesStorePath": "../../data/templates"
  }
}
```

Verification: access `/templates/Payment_request_form.docx` in browser — file downloads correctly.

---

## Enhancement Phase 2.2 — LLM-assisted form/template mapping suggestions

Purpose: reduce manual work when linking policy-mentioned forms to uploaded template files, while keeping final control with the user.

Current manual flow:

```text
1. Upload PDF policy
2. Extract form mentions into data/form-registry-draft.json
3. Upload DOCX/XLSX form templates
4. Manually edit data/form-registry.json to link docx_file/download_path
```

Planned assisted flow:

```text
1. Upload PDF policy
2. Extract draft form mentions into data/form-registry-draft.json
3. Upload DOCX/XLSX form template
4. Detect uploaded file as FORM
5. Compare template filename + extracted template text against draft entries
6. Write suggested matches with confidence scores
7. UI shows Accept / Reject / Choose another
8. Only accepted matches update data/form-registry.json
```

Important rule: do not auto-update `form-registry.json` from the LLM result. The LLM can suggest links, but a user must approve them first.

### Enhancement 2.2.E1 — Form mapping suggestion file

Create a separate suggestion file:

```text
data/form-registry-suggestions.json
```

This file is generated automatically and can be overwritten or updated by the app. It is not the final source of truth.

Suggested schema:

```json
[
  {
    "form_name": "Payment Request Form",
    "aliases": ["payment request", "request for payment"],
    "id": "d0a8d8ecd2c790ee",
    "candidate_template_file": "9ee96f17670f8f0e_Payment request form.docx",
    "candidate_download_path": "/templates/9ee96f17670f8f0e_Payment request form.docx",
    "agent": "ELCA_GENERAL",
    "confidence": 0.93,
    "reason": "Filename and template content both match Payment Request Form.",
    "status": "pending_review"
  }
]
```

Allowed `status` values:

| Status | Meaning |
|---|---|
| `pending_review` | Suggested by the system, waiting for user action |
| `accepted` | User accepted the mapping |
| `rejected` | User rejected the mapping |
| `needs_manual_review` | Confidence was too low to recommend direct acceptance |

---

### Enhancement 2.2.E2 — Candidate matching inputs

When a template file is uploaded and classified as `FORM`, compare it against existing draft registry entries.

Inputs:
- Template filename
- Template download path
- Template extracted text excerpt, first 300-500 words
- Agent
- Entries from `data/form-registry-draft.json`

Matching signals:
- Filename similarity against `form_name` and aliases
- Template text similarity against `form_name` and aliases
- Agent match
- LLM judgment over candidate entries

---

### Enhancement 2.2.E3 — LLM mapping prompt

Use the LLM only to suggest the best candidate, not to write the final registry.

Prompt shape:

```text
You are linking an uploaded form template to exactly one form mentioned in a company policy.

CRITICAL OUTPUT RULES:
- Return ONLY one valid JSON object.
- Do not use markdown fences.
- Do not add explanations before or after the JSON.
- Use double quotes for all JSON property names and string values.
- Use null without quotes when there is no match.
- confidence must be a number between 0 and 1.
- status must be either "pending_review" or "needs_manual_review".
- matched_form_name must exactly match one candidate Form name, or null.

Template filename:
{templateFileName}

Template excerpt:
{templateExcerpt}

Candidate form mentions:
1. Form name: {formName}
   Aliases: {aliases}
   Agent: {agent}

If one candidate matches, return exactly this JSON shape:
{
  "matched_form_name": "Payment Request Form",
  "confidence": 0.93,
  "reason": "Filename and template content both match Payment Request Form.",
  "status": "pending_review"
}

If no candidate matches, return exactly this JSON shape:
{
  "matched_form_name": null,
  "confidence": 0.0,
  "reason": "No strong match found",
  "status": "needs_manual_review"
}
```

Rules:
- `maxTokens: 300`
- Strip markdown fences before parsing JSON
- If parsing fails, log and skip suggestion generation
- Never crash ingestion if suggestion generation fails
- If the LLM request fails or returns unusable mapping JSON, fall back to filename/template-text similarity and write a suggestion only when confidence is at least `0.60`

---

### Enhancement 2.2.E4 — Confidence policy

Use confidence to decide what the UI should show.

| Confidence | Behavior |
|---|---|
| `>= 0.85` | Show as recommended match with Accept / Reject |
| `0.60 - 0.84` | Show as possible match, marked `needs_manual_review` |
| `< 0.60` | Do not recommend direct match; leave unmapped |

Even for `>= 0.85`, the system should not automatically update `form-registry.json`.

---

### Enhancement 2.2.E5 — Review API

Add review endpoints later when the frontend needs them:

```text
GET  /api/form-registry/suggestions
POST /api/form-registry/suggestions/{id}/accept
POST /api/form-registry/suggestions/{id}/reject
POST /api/form-registry/suggestions/{id}/choose-template
```

Accept behavior:
- Copy the accepted suggestion into `data/form-registry.json`
- Fill `docx_file` / `download_path`
- Mark the suggestion as `accepted`

Reject behavior:
- Keep `form-registry.json` unchanged
- Mark the suggestion as `rejected`

Upload response behavior:
- When a template upload produces a suggestion, include it in the `POST /api/ingest` response as `formMappingSuggestion`

---

### Enhancement 2.2.E6 — UI expectation

In the future upload/review UI, show suggestions like:

```text
Payment Request Form
Suggested template: Payment request form.docx
Confidence: 93%
Reason: Filename and content both match the policy mention.

[Accept] [Reject] [Choose another]
```

The final `form-registry.json` remains manually approved, but the user no longer needs to search and map every template from scratch.

---

## Done criteria

- Each supported file type ingests without error
- Text chunks appear in Qdrant with correct `source_file`, `page`, `agent`, `chunk_type`
- Image caption chunks appear with non-empty `text` and valid `image_path`
- Logos and decorative images are filtered out (verified manually)
- Manual and auto agent tagging both work correctly
- Image files accessible via `/images/` static route
- Form template DOCX files accessible via `/templates/` static route
- `form-registry-draft.json` generated after ingesting a PDF with form mentions
- DOCX form templates tagged with `is_form_template: true` in Qdrant
- `form-registry.json` manually completed with `docx_file` links
- Chunker strategy selected and noted in `PROGRESS.md`
