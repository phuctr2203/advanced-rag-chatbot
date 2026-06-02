# Enhancement Phase 1 — Retrieval UX, Language Robustness, and System Visibility

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: improve the production usability of the RAG app after Phase 4 by tightening source/download behavior, making multilingual handling more reliable, adding a document library, and showing current provider/model health in the UI.

---

## Priority order

Implement in this order:

1. E1.1 Suppress misleading sources and downloads when the answer has no relevant context.
2. E1.2 Hybrid language detection using deterministic rules + NTextCat + LLM fallback.
3. E1.3 Documents library API and UI.
4. E1.4 Provider/model display only.
5. E1.5 Provider status display only.

Provider switching and quota tracking are intentionally out of scope for this enhancement phase.

---

## E1.1 — Suppress sources/downloads for no-answer responses

Problem: retrieval can find weakly related chunks, then the LLM correctly says the provided context does not answer the question. The UI should not show citations or form downloads in that case.

Backend requirements:

- Add answer-quality/no-answer detection after streaming completes and before final `[SOURCES]` event is emitted.
- If the answer indicates no relevant information, return:
  ```json
  {
    "sources": [],
    "formDownloads": []
  }
  ```
- Suppress both:
  - source citations
  - standalone form download refs
- Do not suppress sources when the answer gives a grounded answer and includes normal caveats.

Suggested no-answer phrase patterns:

English:
- `provided context does not contain`
- `couldn't find relevant`
- `could not find relevant`
- `does not specify`
- `no information`

Vietnamese:
- `không tìm thấy`
- `không có thông tin`
- `tài liệu không`
- `ngữ cảnh không`

French:
- `je n'ai pas trouvé`
- `ne contient pas`
- `aucune information`

German:
- `konnte keine`
- `enthält keine`
- `keine relevanten`

Implementation note:

- Prefer a small `AnswerGroundingService` or `NoAnswerDetectorService`.
- Keep it deterministic for this phase.
- Later phases can replace it with structured LLM output or a grounding classifier.

Verification:

- Ask `How do I submit a payment request?` when only the form template is retrieved but the process is not described.
- If the answer says the process is not in context, no download chip should appear.
- Ask a valid payment-form query where the answer specifically identifies the form; download refs may still appear.

---

## E1.2 — Hybrid language detection

Problem: short Vietnamese questions without existing policy keywords can be detected as English, causing English responses.

Backend requirements:

- Replace the current `string Detect(string message)` behavior with a richer result:
  ```csharp
  public class LanguageDetectionResult
  {
      public string Language { get; set; } = "en";
      public string Method { get; set; } = "default";
      public float Confidence { get; set; }
  }
  ```
- Detection order:
  1. Exact smalltalk language hints.
  2. Unicode/diacritic heuristics, especially Vietnamese.
  3. Language keyword hints.
  4. NTextCat.
  5. LLM fallback if result is unsupported or low confidence.
  6. Default to `en`.
- Supported languages remain:
  - `en`
  - `vi`
  - `fr`
  - `de`
- Add Vietnamese detection for common non-policy phrasing:
  - `tòa nhà`
  - `toà nhà`
  - `làm việc`
  - `hàng ngày`
  - `mấy giờ`
  - `thời gian`
- LLM fallback prompt:
  ```text
  Detect the language of the user message.
  Supported languages:
  - en: English
  - vi: Vietnamese
  - fr: French
  - de: German

  Respond with ONLY one code: en, vi, fr, or de.

  User message: {message}
  ```
- Unexpected LLM response defaults to `en`.

Prompt builder requirements:

- Pass both code and language name to the answer prompt.
- Use strict wording:
  ```text
  The detected user language is Vietnamese (vi).
  You MUST write the full answer in Vietnamese. Do not answer in another language.
  ```

Verification:

- `thời gian làm việc hàng ngày của tòa nhà là mấy giờ` responds in Vietnamese.
- Annual leave questions still respond in EN, VI, FR, DE.
- Smalltalk still uses static localized responses.

---

## E1.3 — Documents library

Problem: the Documents page uploads files but does not show what is currently indexed.

Backend requirements:

- Add `GET /api/documents`.
- Add `DELETE /api/documents/{sourceFile}` to remove all indexed chunks for a selected document.
- Read Qdrant `policy_docs` payloads by scrolling points.
- Group chunks by `source_file`.
- Return one item per document:
  ```csharp
  public class DocumentSummary
  {
      public string SourceFile { get; set; } = string.Empty;
      public string Agent { get; set; } = string.Empty;
      public string FileType { get; set; } = string.Empty;
      public int ChunkCount { get; set; }
      public int PageCount { get; set; }
      public bool HasImages { get; set; }
      public bool HasFormTemplate { get; set; }
  }
  ```
- Page count should be distinct count of payload `page`.
- `HasImages` is true if any chunk has `chunk_type = "image_caption"` or non-empty `image_path`.
- `HasFormTemplate` is true if any chunk has `is_form_template = true`.

Frontend requirements:

- Documents page shows a library section.
- Display:
  - filename
  - agent
  - file type
  - chunks
  - pages
  - images yes/no
  - form template yes/no
- Refresh library after upload succeeds.
- Allow deleting an indexed document from the library after confirmation.
- Show loading/error states.
Please follow this design on Figma https://www.figma.com/make/Za45aQJW7dUgjfA39Z5QYZ/Chat-and-Documents-Pages?t=0K1XBT6IPFew3iQh-0&preview-route=%2Fdocuments

Verification:

- Upload a document and see it appear in the library.
- Existing Qdrant documents are listed on page load.

---

## E1.4 — Provider/model display

Scope: display current configured provider and model only. Do not implement switching in this phase.

Backend requirements:

- Add `GET /api/providers/current`.
- Return:
  ```csharp
  public class CurrentProviderResponse
  {
      public string LlmProvider { get; set; } = string.Empty;
      public string LlmModel { get; set; } = string.Empty;
      public string VisionProvider { get; set; } = string.Empty;
      public string VisionModel { get; set; } = string.Empty;
      public string EmbeddingProvider { get; set; } = "TEI";
      public string EmbeddingBaseUrl { get; set; } = string.Empty;
  }
  ```
- Values should come from current configuration/options.

Frontend requirements:

- Show provider/model in the sidebar or Documents status panel.
- Keep display compact.

Verification:

- UI shows active LLM provider/model and embedding endpoint.

---

## E1.5 — Provider status display

Scope: show health/status only. Do not implement quota tracking in this phase.

Backend requirements:

- Add `GET /api/providers/status`.
- Check:
  - LLM provider reachable
  - embedding provider reachable
  - Qdrant reachable
- Return:
  ```csharp
  public class ProviderStatusResponse
  {
      public ServiceStatus Llm { get; set; } = new();
      public ServiceStatus Embedding { get; set; } = new();
      public ServiceStatus Qdrant { get; set; } = new();
  }

  public class ServiceStatus
  {
      public string Name { get; set; } = string.Empty;
      public bool Healthy { get; set; }
      public string Message { get; set; } = string.Empty;
  }
  ```
- For local Ollama, health can be a lightweight HTTP check or a short model call if needed.
- Do not call billing/quota APIs.

Frontend requirements:

- Show status chips:
  - LLM
  - Embedding
  - Qdrant
- Use healthy/unhealthy visual states.
- Show `quota unavailable` only if a quota field is shown; otherwise omit quota entirely.

Verification:

- UI shows healthy status when services are running.
- UI shows an error/unhealthy state if a service is unavailable.

---

## Done criteria

- No-answer responses do not show misleading citations or form downloads.
- Vietnamese short/non-policy phrasing is answered in Vietnamese.
- All four supported languages still pass the annual-leave test.
- Documents page lists indexed documents from Qdrant.
- Upload refreshes the document library.
- UI displays current provider/model.
- UI displays health status for LLM, embedding, and Qdrant.
