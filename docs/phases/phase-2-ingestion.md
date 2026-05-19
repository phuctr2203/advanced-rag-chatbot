# Phase 2 — Document Ingestion Pipeline

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: parse all supported document types, caption images via vision model, chunk text, classify agent domain, embed chunks, and store in Qdrant with full metadata.

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

## Task 2.2 — PDF parser (image extraction + captioning)

Extend `PdfParserService` to handle images on each page.

For each image found via `page.GetImages()`:

1. Skip if width or height < 100 px (decorative)
2. Save bytes to `{ImageStorePath}/{docName}/page{N}_img{I}.png`
3. Call `ImageCaptioningService.CaptionAsync(imageBytes, pageText)`
4. Create a separate `ParsedChunk` with:
   - `Text` = caption returned by vision model
   - `ChunkType` = `"image_caption"`
   - `ImagePath` = `/images/{docName}/page{N}_img{I}.png`
   - `PageNumber`, `SourceFile`, `FileType`, `Agent` same as page

`ImageCaptioningService` prompt to vision model:
```
This image appears in a company policy document called "{sourceFile}", page {page}.
The surrounding text on this page discusses: "{first150charsOfPageText}".
Describe what this image shows in 2-3 sentences. Focus on content relevant to company policies.
```

Rules:
- If captioning fails, log the error and skip — do not crash ingestion
- Empty captions are skipped (not stored)

Verification: parse a PDF with images, confirm image files saved to disk and caption chunks created with non-empty text.

---

## Task 2.3 — PPTX → PDF conversion

Implement `FileConversionService.ToPdfAsync(string filePath)`:

```bash
libreoffice --headless --convert-to pdf --outdir {tempDir} {filePath}
```

- Route output PDF into `PdfParserService`
- Slide number = page number in citations

Verification: convert a PPTX, confirm output PDF page count equals slide count.

---

## Task 2.4 — DOCX parser

Implement `DocxParserService` using `DocumentFormat.OpenXml`.

Behavior:
- Extract non-empty paragraphs from `body.Elements<Paragraph>()`
- Track page breaks via `Break` elements — increment page counter when found
- Estimate page number if no explicit page break
- Extract images from `ImagePart` relationships (document-level)
- Save images and call `ImageCaptioningService` same as PDF flow
- Attach all document images to all chunks (DOCX has no per-page image API)

Verification: parse a DOCX with paragraphs and embedded images.

---

## Task 2.5 — DOC → DOCX conversion

Implement `FileConversionService.ToDocxAsync(string filePath)`:

```bash
libreoffice --headless --convert-to docx --outdir {tempDir} {filePath}
```

Route output DOCX into `DocxParserService`.

Verification: converted DOCX parses and preserves useful text.

---

## Task 2.6 — XLSX parser

Implement `XlsxParserService` using `ClosedXML`.

Rules:
- Do NOT dump raw cell values
- Treat each sheet as one page
- Reconstruct each row as readable prose from column headers + values:
  ```
  Field: Request Type — Value: Laptop Purchase — Description: Equipment request form
  ```
- Filter empty rows
- No image captioning needed for form templates

Verification: internal purchasing form produces readable policy context sentences.

---

## Tasks 2.7–2.9 — Text chunking strategies

Implement `TextChunkerService` with a selectable strategy via `Ingestion:ChunkingStrategy` config.

### Strategy A — FixedSize

- Split by word count: 400 words per chunk
- 80-word overlap between chunks
- Drop chunks under 30 words

### Strategy B — ParagraphBoundary *(default)*

- Split on blank lines and heading-like patterns first
- Keep paragraphs intact when under 400 words
- Fall back to FixedSize for paragraphs exceeding 400 words

### Strategy C — SentenceWindow

- Split on sentence endings: `.`, `?`, `!`, `。`
- Accumulate sentences until 400-word target
- Carry last 1–2 sentences into the next chunk as overlap

All strategies must:
- Skip `image_caption` chunks — they are not re-chunked
- Preserve all `ParsedChunk` metadata fields on output chunks

---

## Task 2.10 — Chunker evaluation

Before ingesting all 20 documents, evaluate strategies:

1. Ingest the same 2–3 policy documents with each strategy into separate Qdrant collections (e.g. `policy_docs_fixed`, `policy_docs_para`, `policy_docs_sentence`)
2. Ask 5 representative questions against each collection
3. Compare: coherence of retrieved chunks, answer completeness, citation precision
4. Choose the best strategy and record the decision in `.claude/PROGRESS.md` under the chunker note

---

## Task 2.11 — Manual document classifier

Accept optional `agent` query param on the ingest endpoint:

```
POST /api/ingest?agent=ELCA_HR
```

Valid values: `ELCA_HR`, `ELCA_GENERAL`, `CII_TOWER_SUPPORT`

If provided → skip LLM classification, apply agent directly to all chunks.

---

## Task 2.12 — LLM auto-classify fallback

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
- One call per document (not per chunk)
- Unexpected response defaults to `ELCA_GENERAL`
- Log the chosen category

---

## Task 2.13 — Ingestion orchestrator

`DocumentIngestionService` flow:

```
1. Validate file extension
2. Convert if needed (DOC→DOCX, PPTX→PDF)
3. Parse file → text chunks + image caption chunks
4. Determine agent (manual param OR LLM fallback)
5. Apply agent to all chunks
6. Run text chunks through chunker (image_caption chunks skipped)
7. Embed all chunks in batches of 32
8. Upsert to Qdrant with full payload
9. Return { filename, agent, chunkCount }
```

---

## Task 2.14 — Ingest endpoint

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

---

## Task 2.15 — Static image serving

`Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});
```

Verification: upload a PDF with images, access `/images/{docName}/page1_img0.png` in browser and confirm it loads.

---

## Done criteria

- Each supported file type ingests without error
- Text chunks appear in Qdrant with correct `source_file`, `page`, `agent`, `chunk_type`
- Image caption chunks appear with non-empty `text` and valid `image_path`
- Manual and auto agent tagging both work correctly
- Image files accessible via `/images/` static route
- Chunker strategy selected and noted in `PROGRESS.md`