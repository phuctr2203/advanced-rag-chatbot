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

Already indexed response:

```json
{
  "message": "leave_policy.pdf skipped because it already exists in Qdrant.",
  "fileName": "leave_policy.pdf",
  "status": "skipped",
  "chunks": 0
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

## POST /api/ingest/batch

Ingest multiple documents into Qdrant in one request.

Files already present in Qdrant by `source_file` are returned with `status: "skipped"`.

### Request

```text
POST /api/ingest/batch?agent=ELCA_GENERAL
Content-Type: multipart/form-data
```

`agent` query parameter optional. If omitted, each document is classified automatically.

Form fields:

| Field | Type | Required | Notes |
|---|---|---|---|
| `files` | file[] | yes | Repeat this field once per document |

Allowed extensions are the same as `POST /api/ingest`: `.pdf`, `.docx`, `.doc`, `.xlsx`, `.pptx`.

### Success response

The endpoint returns `200 OK` when the batch request was processed, even if some individual files failed validation or ingestion. Check `successCount`, `failedCount`, and each item status.

```json
{
  "message": "Batch ingestion completed: 2 succeeded, 1 failed.",
  "successCount": 2,
  "failedCount": 1,
  "skippedCount": 0,
  "totalChunks": 84,
  "results": [
    {
      "fileName": "leave_policy.pdf",
      "status": "success",
      "agent": "ELCA_HR",
      "chunks": 42,
      "document": {
        "originalFileName": "leave_policy.pdf",
        "storedFileName": "9ee96f17670f8f0e_leave_policy.pdf",
        "url": "/documents/20260608/9ee96f17670f8f0e_leave_policy.pdf",
        "sha256": "9ee96f17670f8f0e...",
        "reused": false
      },
      "template": null,
      "formMappingSuggestion": null
    },
    {
      "fileName": "notes.txt",
      "status": "failed",
      "error": "Unsupported file type '.txt'."
    }
  ]
}
```

## POST /api/ingest/folder

Ingest all supported documents from a local folder path on the API server machine.

This endpoint is useful for bulk loading an existing document folder without uploading each file through multipart form data.
Files already present in Qdrant by `source_file` are returned with `status: "skipped"`.

### Request

```http
POST /api/ingest/folder?agent=ELCA_GENERAL
Content-Type: application/json
```

`agent` query parameter optional. If omitted, each document is classified automatically.

```json
{
  "folderPath": "C:\\Policies\\Batch1",
  "recursive": false
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `folderPath` | string | yes | Folder path visible to the API process |
| `recursive` | boolean | no | If `true`, include files in subfolders |

Allowed extensions are the same as `POST /api/ingest`: `.pdf`, `.docx`, `.doc`, `.xlsx`, `.pptx`.

Every supported file is first copied into normal upload storage:

```text
data/uploads/{yyyyMMdd}/{first16Sha256}_{originalFileName}
```

Then it is processed by the same ingestion pipeline as regular uploads.

### Success response

The endpoint returns `200 OK` when the folder request was processed, even if some individual files failed or were skipped.

Unsupported file extensions are marked as `skipped`.

```json
{
  "message": "Folder ingestion completed: 2 succeeded, 1 failed, 3 skipped.",
  "folderPath": "C:\\Policies\\Batch1",
  "recursive": false,
  "successCount": 2,
  "failedCount": 1,
  "skippedCount": 3,
  "totalChunks": 84,
  "results": [
    {
      "fileName": "leave_policy.pdf",
      "path": "C:\\Policies\\Batch1\\leave_policy.pdf",
      "status": "success",
      "agent": "ELCA_HR",
      "chunks": 42,
      "document": {
        "originalFileName": "leave_policy.pdf",
        "storedFileName": "9ee96f17670f8f0e_leave_policy.pdf",
        "url": "/documents/20260608/9ee96f17670f8f0e_leave_policy.pdf",
        "sha256": "9ee96f17670f8f0e...",
        "reused": false
      },
      "template": null,
      "formMappingSuggestion": null
    },
    {
      "fileName": "notes.txt",
      "path": "C:\\Policies\\Batch1\\notes.txt",
      "status": "skipped",
      "error": "Unsupported file type '.txt'."
    }
  ]
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
