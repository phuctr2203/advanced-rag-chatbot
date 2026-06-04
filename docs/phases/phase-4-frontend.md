# Phase 4 — Frontend

Goal: React UI for streaming chat, source citations, image display, and document upload.

## Task 4.1 — React scaffold

Create Vite React TypeScript app in `Web/`.

```bash
npm create vite@latest Web -- --template react-ts
cd Web && npm install axios
```

Recommended extra packages only if needed:

- none for v1; use plain React state and fetch/EventSource-compatible streaming.

## Task 4.2 — Chat UI

Required components:

- Message input.
- Send button.
- Conversation history.
- User/bot message styling.
- Loading indicator before first token.
- Error message area.

State shape:

```ts
type ChatMessage = {
  role: 'user' | 'assistant';
  content: string;
  sources?: SourceRef[];
};
```

## Task 4.3 — SSE streaming rendering

Browser `EventSource` does not support POST body. Use one of these approaches:

1. Use `fetch` with `ReadableStream` for POST `/api/chat`.
2. Change API to accept GET with query string only if acceptable.

Preferred: `fetch` streaming with POST, because API contract is POST.

Requirements:

- Append tokens to current assistant message as chunks arrive.
- Do not wait for full response.
- Handle final source event separately.
- Stop loading indicator after first token.

## Task 4.4 — Source citation panel

Display below assistant messages.

Each source chip shows:

```text
{file} — Page {page}
```

Click behavior optional for v1.

## Task 4.5 — Image display

If `source.imagePaths` has values:

- Render images below matching citation.
- Use `max-width: 100%`.
- Add alt text with filename and page.
- Do not block answer rendering while images load.

## Task 4.6 — Document upload UI

Controls:

- File picker accepting `.pdf,.docx,.doc,.xlsx,.pptx`.
- Agent selector:
  - Auto-detect
  - ELCA HR
  - ELCA General
  - CII Tower Support
- Upload button.
- Loading state.
- Success message with agent and chunk count.
- If upload response includes `formMappingSuggestion`, show a compact suggestion review card.
- Error message for bad upload.

Endpoint behavior:

- Auto-detect: `POST /api/ingest`
- Manual: `POST /api/ingest?agent=ELCA_HR`

Upload response may include:

```ts
type FormMappingSuggestion = {
  id: string;
  formName: string;
  aliases: string[];
  candidateTemplateFile: string;
  candidateDownloadPath: string;
  agent: string;
  confidence: number;
  reason: string;
  status: 'pending_review' | 'needs_manual_review' | 'accepted' | 'rejected';
};
```

When present, display:

```text
{formName}
Suggested template: {candidateTemplateFile}
Confidence: {confidencePercent}
Reason: {reason}
```

Actions:
- Accept
- Reject
- Choose another

Do not update UI as final-linked until the accept API succeeds.

## Task 4.7 — Form mapping suggestion review UI

Purpose: let the user approve or reject LLM-assisted template mapping suggestions generated during Phase 2 ingestion.

Add a review area in the upload/system side panel:

- Show pending suggestions from `GET /api/form-registry/suggestions`
- Group by status:
  - Recommended (`pending_review`)
  - Needs review (`needs_manual_review`)
  - Accepted
  - Rejected
- Show confidence as a percentage
- Show the LLM/fallback reason
- Show candidate template file and download path
- Provide actions for each pending/needs-review suggestion:
  - Accept
  - Reject
  - Choose another

Endpoint behavior:

```text
GET  /api/form-registry/suggestions
POST /api/form-registry/suggestions/{id}/accept
POST /api/form-registry/suggestions/{id}/reject
POST /api/form-registry/suggestions/{id}/choose-template
```

Accept behavior:
- Call accept endpoint
- Move card to Accepted state
- Show that `form-registry.json` has been updated

Reject behavior:
- Call reject endpoint
- Move card to Rejected state
- Do not remove it immediately; keep it visible for traceability

Choose another behavior:
- Let user enter/select `candidateTemplateFile` and `candidateDownloadPath`
- Call choose-template endpoint
- Return card to `pending_review`

UI copy:

```text
This is a suggested mapping. Review before accepting.
```

Do not imply the LLM result is final. The final source of truth is still the accepted entry in `form-registry.json`.

## Layout suggestion

Two-column layout:

- Main: chat panel.
- Side: document upload + system status.

Mobile:

- Stack upload panel above or below chat.

## Done criteria

- User can ask question and see tokens stream.
- Sources appear below answer.
- Source images render when present.
- User can upload document with auto-detect and manual agent selection.
- Upload result shows selected/detected agent and chunk count.
- User can review form/template mapping suggestions with confidence score.
- User can accept, reject, or choose another template for a mapping suggestion.
