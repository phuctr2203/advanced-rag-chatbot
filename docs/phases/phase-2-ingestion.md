# Phase 2 — Document Ingestion Pipeline

Goal: parse supported document types, chunk text, classify agent domain, embed chunks, and store metadata in Qdrant.

## Supported file types

- `.pdf`
- `.pptx` converted to PDF through LibreOffice
- `.docx`
- `.doc` converted to DOCX through LibreOffice
- `.xlsx`

## Data rules

Every chunk must carry:

- `Text`
- `SourceFile`
- `PageNumber`
- `ChunkIndex`
- `FileType`
- `Agent`
- `ImagePaths`

Metadata must survive parse → chunk → embed → upsert.

## Task 2.1 — PDF parser

Implement `PdfParserService` using PdfPig.

Behavior:

- Read text per page using `page.GetWords()`.
- Extract images using `page.GetImages()`.
- Skip decorative images smaller than 100×100 pixels.
- Save images under `data/images/{docName}/page{page}_img{index}.png`.
- Store URL path as `/images/{docName}/page{page}_img{index}.png`.
- Attach page image paths to chunks from same page.

Verification: parse PDF with images; text non-empty; image files load from disk.

## Task 2.2 — PPTX conversion

Implement `FileConversionService` method for PPTX:

```bash
libreoffice --headless --convert-to pdf --outdir {outputDir} {filePath}
```

Route output PDF into PDF parser. Slide number becomes citation page number.

Verification: converted PDF page count equals slide count.

## Task 2.3 — DOCX parser

Implement `DocxParserService` with OpenXML.

Behavior:

- Extract non-empty paragraphs.
- Track page breaks through `Break` elements where possible.
- Estimate page if explicit page breaks are absent.
- Extract images from `ImagePart` relationships.
- Store images under `data/images/{docName}/...`.
- Attach document-level image paths to all chunks.

Verification: parse DOCX with paragraphs and images.

## Task 2.4 — DOC conversion

Use LibreOffice headless:

```bash
libreoffice --headless --convert-to docx --outdir {outputDir} {filePath}
```

Route output DOCX into DOCX parser.

Verification: converted DOCX parses and preserves useful text.

## Task 2.5 — XLSX parser

Implement `XlsxParserService` with ClosedXML.

Rules:

- Do not dump raw cell values.
- Treat each sheet as one page.
- Build readable row prose from headers and values.
- Filter empty rows.

Example output:

```text
Field: Request Type — Value: Laptop Purchase — Description: Equipment request form
```

Verification: internal form becomes readable policy context.

## Tasks 2.6–2.8 — Text chunking strategies

Implement `TextChunkerService` with selectable strategy:

- `FixedSize`
- `ParagraphBoundary`
- `SentenceWindow`

Config:

```json
{
  "Ingestion": {
    "ChunkingStrategy": "ParagraphBoundary",
    "ChunkSizeWords": 400,
    "ChunkOverlapWords": 80,
    "MinimumChunkWords": 30
  }
}
```

### Strategy A — FixedSize

- 400 words per chunk.
- 80-word overlap.
- Drop chunks under 30 words.

### Strategy B — ParagraphBoundary

- Split on blank lines and heading-like boundaries.
- Keep paragraphs intact when possible.
- Fall back to fixed-size for long paragraphs.

### Strategy C — SentenceWindow

- Split on sentence endings: `.`, `?`, `!`, `。`.
- Accumulate sentences to target size.
- Carry last 1–2 sentences into next chunk.

## Task 2.9 — Chunker evaluation

Evaluate with real documents before full ingestion.

Test method:

1. Ingest same document using each strategy into separate test collections.
2. Ask 5 representative questions.
3. Compare coherence, answer completeness, and citation precision.
4. Choose default strategy and document reason in `docs/PROGRESS.md` notes.

## Task 2.10 — Manual document classifier

`POST /api/ingest?agent=ELCA_HR`

Valid values:

- `ELCA_HR`
- `ELCA_GENERAL`
- `CII_TOWER_SUPPORT`

If provided, skip LLM classification.

## Task 2.11 — LLM auto-classify fallback

When `agent` missing, classify first 500 words.

Prompt:

```text
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
- One call per document.
- Unexpected response defaults to `ELCA_GENERAL`.
- Log chosen category.

## Task 2.12 — Ingestion orchestrator

`DocumentIngestionService` flow:

1. Validate extension.
2. Convert DOC/PPTX if needed.
3. Parse file and extract images.
4. Determine agent from manual param or LLM fallback.
5. Chunk parsed pages using configured strategy.
6. Apply agent to all chunks.
7. Embed chunks in batches.
8. Upsert chunks and vectors to Qdrant.
9. Return filename, agent, and chunk count.

## Task 2.13 — Ingest endpoint

Endpoint:

```text
POST /api/ingest?agent=ELCA_HR
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

Return 400 for unsupported types or invalid agent.

## Task 2.14 — Static image serving

Configure in `Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});
```

Verification: browser loads `/images/{docName}/page1_img0.png`.

## Done criteria

- Each supported file type ingests successfully.
- Chunks appear in Qdrant with correct payload.
- Manual and auto agent tagging work.
- Image paths load through static route.
