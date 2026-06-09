# Enhancement Phase 1 — Retrieval UX, Language Robustness, and System Visibility

> **Agent:** when tasks are completed, update checkboxes in `.claude/PROGRESS.md` — not here.

Goal: improve the production usability of the RAG app after Phase 4 by tightening source/download behavior, making multilingual handling more reliable, adding a document library, and showing current provider/model health in the UI.

---

## Priority order

Implement in this order:

3. E1.3 Documents library API and UI.
4. E1.4 Provider/model display only.
5. E1.5 Provider status display only.

Provider switching and quota tracking are intentionally out of scope for this enhancement phase.

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
