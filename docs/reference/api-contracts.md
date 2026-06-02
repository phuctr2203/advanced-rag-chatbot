# API Contracts Reference

## POST /api/ingest

Ingest document into Qdrant.

### Request

```text
POST /api/ingest?agent=ELCA_HR
Content-Type: multipart/form-data
```

`agent` query parameter optional.

Valid values:

- `ELCA_HR`
- `ELCA_GENERAL`
- `CII_TOWER_SUPPORT`

Form fields:

| Field | Type | Required |
|---|---|---|
| `file` | file | yes |

Allowed extensions:

- `.pdf`
- `.docx`
- `.doc`
- `.xlsx`
- `.pptx`

### Success response

```json
{
  "message": "leave_policy.pdf ingested successfully",
  "agent": "ELCA_HR",
  "chunks": 42
}
```

### Error responses

Unsupported type:

```json
{
  "error": "Unsupported file type. Please upload PDF, DOCX, DOC, XLSX, or PPTX."
}
```

Invalid agent:

```json
{
  "error": "Invalid agent. Valid values are ELCA_HR, ELCA_GENERAL, CII_TOWER_SUPPORT."
}
```

## POST /api/chat

Stream answer to policy question.

### Request

```http
POST /api/chat
Content-Type: application/json
Accept: text/event-stream
```

```json
{
  "message": "What is the annual leave policy?"
}
```

### SSE response

Headers:

```http
Content-Type: text/event-stream
Cache-Control: no-cache
```

Token event:

```text
data: The annual leave policy states

```

Final sources event:

```text
event: sources
data: [{"file":"leave_policy.pdf","page":3,"imagePaths":["/images/leave_policy/page3_img0.png"]}]

```

Error event:

```text
event: error
data: {"error":"The policy search service is unavailable. Please try again later."}

```

## GET /images/{path}

Serve extracted images.

Example:

```text
GET /images/leave_policy/page3_img0.png
```

Backed by configured `Ingestion:ImageStorePath`.

## GET /api/documents

List documents currently indexed in Qdrant.

### Success response

```json
[
  {
    "sourceFile": "leave_policy.pdf",
    "agent": "ELCA_HR",
    "fileType": "pdf",
    "chunkCount": 42,
    "pageCount": 6,
    "hasImages": false,
    "hasFormTemplate": false
  }
]
```

## DELETE /api/documents/{sourceFile}

Delete all Qdrant chunks indexed under the selected source filename.

Example:

```text
DELETE /api/documents/leave_policy.pdf
```

Successful deletion returns `204 No Content`.

## GET /api/providers/current

Return the configured LLM, vision, and embedding provider details.

## GET /api/providers/status

Return lightweight reachability checks for the active LLM provider, TEI embedding service, and Qdrant.

## POST /api/evaluation/query

Run a non-streaming query through the production RAG pipeline and return trace metadata for
offline evaluation. The route returns `404` unless `Evaluation:Enabled` is `true`.

### Request

```json
{
  "question": "What is the annual leave entitlement?"
}
```

### Response fields

- detected language, method, and confidence
- normalized intent
- generated answer
- whether retrieval ran
- no-answer status
- retrieved chunk text, score, filename, page, chunk type, image path, agent, and form flag
- structured sources and form downloads

## Future MCP tools

### search_policy

Input:

```json
{
  "query": "annual leave policy",
  "agent": "ELCA_HR"
}
```

Output:

```json
{
  "results": [
    {
      "text": "...",
      "sourceFile": "leave_policy.pdf",
      "page": 3,
      "score": 0.82,
      "imagePaths": []
    }
  ]
}
```

### list_documents

Input:

```json
{}
```

Output:

```json
{
  "documents": [
    {
      "sourceFile": "leave_policy.pdf",
      "agent": "ELCA_HR",
      "fileType": "pdf",
      "chunks": 42
    }
  ]
}
```

### ingest_document

Input:

```json
{
  "path": "C:\\Policies\\leave.pdf",
  "agent": "ELCA_HR"
}
```
