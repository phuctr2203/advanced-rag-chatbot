# Enhancement — Ingestion Deduplication & Document Versioning

> **When to implement:** After Phase 2 ingestion pipeline is working end-to-end.
> Feed this file to Claude Code when starting this enhancement.

---

## Problem

Every upload generates new random UUIDs for chunks. Qdrant treats them as brand new points regardless of whether the file was already ingested. This causes:

- **Duplicate chunks** — same document uploaded twice = double the chunks, double retrieval noise
- **Stale content** — updated document uploaded = old and new versions mixed in Qdrant
- **Cross-day corruption** — re-ingesting on a different day compounds the problem silently

---

## Solution overview

Three changes to `DocumentIngestionService` and `VectorStoreService`:

```
File uploaded
      ↓
Compute file hash (MD5)
      ↓
Hash unchanged? → return "skipped, already up to date"
      ↓
Delete ALL existing Qdrant chunks for this source_file
      ↓
Parse + chunk + embed + classify (existing pipeline)
      ↓
Upsert with deterministic chunk IDs
      ↓
Return { chunks, replaced_chunks, is_update, file_hash }
```

---

## Implementation details

### 1. Deterministic chunk IDs

Replace `Guid.NewGuid()` with a SHA256-based ID derived from file content identity.
Same file + same chunk position = same ID = Qdrant upserts (overwrites) instead of inserting duplicate.

```csharp
private static string GenerateChunkId(ParsedChunk chunk)
{
    var raw = $"{chunk.SourceFile}::{chunk.PageNumber}::{chunk.ChunkIndex}";
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    return new Guid(bytes[..16]).ToString();
}
```

Use in `VectorStoreService.UpsertAsync`:
```csharp
Id = new PointId { Uuid = GenerateChunkId(chunk) }
```

### 2. Delete before upsert

Before ingesting, delete all existing Qdrant points for the same `source_file`.
This handles document updates — removed pages, restructured content, fewer chunks — so no orphan chunks remain.

```csharp
await _qdrant.DeleteAsync("policy_docs",
    new Filter
    {
        Must = [new Condition
        {
            Field = new FieldCondition
            {
                Key = "source_file",
                Match = new Match { Value = fileName }
            }
        }]
    },
    cancellationToken: ct
);
```

> This must run **before** the new chunks are upserted, not after.

### 3. File hash check (skip unchanged files)

Compute MD5 of the uploaded file bytes. Compare against the hash stored in the last ingestion.
If identical → skip re-ingestion entirely and return early.

```csharp
private static string ComputeFileHash(string filePath)
{
    using var md5 = MD5.Create();
    using var stream = File.OpenRead(filePath);
    var hash = md5.ComputeHash(stream);
    return Convert.ToHexString(hash).ToLower();
}
```

Store the hash in Qdrant payload on every ingestion:
```json
{
  "source_file": "leave_policy.pdf",
  "file_hash": "a3f4b2c1d9e8...",
  "ingested_at": "2026-05-14T08:30:00Z",
  ...
}
```

Retrieve stored hash before ingesting:
```csharp
// Query one chunk from this file to get stored hash
var existing = await _qdrant.ScrollAsync("policy_docs",
    filter: new Filter { Must = [...match source_file...] },
    limit: 1
);

var storedHash = existing.FirstOrDefault()
    ?.Payload["file_hash"]?.StringValue;

if (storedHash == newHash)
    return new IngestResult { Message = $"{fileName} unchanged — skipped", Skipped = true };
```

### 4. Updated ingest response

Return metadata so the frontend and logs can show what happened:

```csharp
public class IngestResult
{
    public string Message { get; set; }
    public string Agent { get; set; }
    public int Chunks { get; set; }
    public int ReplacedChunks { get; set; }   // how many old chunks were deleted
    public bool IsUpdate { get; set; }         // was this a re-upload of existing file?
    public bool Skipped { get; set; }          // true if file hash unchanged
    public string FileHash { get; set; }
    public string IngestedAt { get; set; }
}
```

Example responses:

```json
// First upload
{
  "message": "leave_policy.pdf ingested successfully",
  "agent": "ELCA_HR",
  "chunks": 44,
  "replacedChunks": 0,
  "isUpdate": false,
  "skipped": false
}

// Re-upload with changes
{
  "message": "leave_policy.pdf updated successfully",
  "agent": "ELCA_HR",
  "chunks": 47,
  "replacedChunks": 44,
  "isUpdate": true,
  "skipped": false
}

// Re-upload unchanged file
{
  "message": "leave_policy.pdf unchanged — skipped",
  "skipped": true
}
```

---

## Behaviour matrix

| Scenario | What happens |
|---|---|
| First upload | No delete step, upsert 44 chunks |
| Re-upload same file, no changes | Hash match → skip, return early |
| Re-upload same file, content changed | Delete 44 old chunks → upsert 47 new chunks |
| Upload on Day 1, re-upload Day 5 (updated) | Delete Day 1 chunks → upsert Day 5 chunks |
| Two different files with same name | Treated as update — old file replaced |
| Upload file, change agent manually | Not detected by hash — requires force re-ingest flag |

---

## Edge case — force re-ingest

Add an optional `force` query param to bypass the hash check when needed
(e.g. user changed the agent classification and wants to re-ingest with the new tag):

```
POST /api/ingest?agent=ELCA_HR&force=true
```

```csharp
if (!forceReIngest && storedHash == newHash)
    return new IngestResult { Skipped = true, ... };
```

---

## Files to modify

| File | Change |
|---|---|
| `Services/Ingestion/DocumentIngestionService.cs` | Add hash check, delete-before-upsert, return IngestResult |
| `Services/Shared/VectorStoreService.cs` | Add DeleteBySourceFileAsync, GenerateChunkId |
| `Models/IngestResult.cs` | New model — replace plain string return |
| `Controllers/IngestController.cs` | Return IngestResult from endpoint |

---

## Checklist

- [ ] `GenerateChunkId` — SHA256 hash of `filename::page::chunkIndex`, returns deterministic UUID
- [ ] `ComputeFileHash` — MD5 of file bytes, returns hex string
- [ ] `VectorStoreService.DeleteBySourceFileAsync` — delete all Qdrant points matching `source_file` filter
- [ ] `DocumentIngestionService` — retrieve stored hash from existing Qdrant points before ingesting
- [ ] `DocumentIngestionService` — skip ingestion and return early if hash matches (file unchanged)
- [ ] `DocumentIngestionService` — call `DeleteBySourceFileAsync` before upserting new chunks
- [ ] `DocumentIngestionService` — store `file_hash` and `ingested_at` in Qdrant payload
- [ ] `IngestResult` model — add `ReplacedChunks`, `IsUpdate`, `Skipped`, `FileHash`, `IngestedAt`
- [ ] `IngestController` — add `?force=true` param to bypass hash check
- [ ] **Verification:** upload same file twice → chunk count stays the same, no duplicates in Qdrant
- [ ] **Verification:** upload updated file → old chunk count deleted, new count stored
- [ ] **Verification:** upload unchanged file → returns skipped response, Qdrant unchanged
- [ ] **Verification:** check Qdrant dashboard — `file_hash` and `ingested_at` present in payload

---

## Notes for Claude Code

- `DeleteBySourceFileAsync` must complete before `UpsertAsync` is called — do not run in parallel
- The hash check uses a Qdrant scroll query to retrieve one existing chunk — this is a read before write, handle the case where no chunks exist (first upload) gracefully
- `ingested_at` should always be stored as UTC ISO 8601 string: `DateTime.UtcNow.ToString("O")`
- Do not change the chunking or embedding logic — this enhancement only affects ID generation, deletion, and the ingest response model