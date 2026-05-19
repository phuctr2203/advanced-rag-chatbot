# Data Models Reference

## ParsedChunk

```csharp
public class ParsedChunk
{
    public string Text { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string Agent { get; set; } = "ELCA_GENERAL";
    public List<string> ImagePaths { get; set; } = [];
}
```

## ChatRequest

```csharp
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
```

Optional later fields:

```csharp
public string? ConversationId { get; set; }
```

Do not implement conversation history in v1 unless needed.

## ChatResponse

Used for non-streaming tests or final structured response.

```csharp
public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceRef> Sources { get; set; } = [];
}
```

## SourceRef

```csharp
public class SourceRef
{
    public string File { get; set; } = string.Empty;
    public int Page { get; set; }
    public List<string> ImagePaths { get; set; } = [];
}
```

## IngestResponse

```csharp
public class IngestResponse
{
    public string Message { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public int Chunks { get; set; }
}
```

## Agent values

```csharp
public static class AgentDomains
{
    public const string Hr = "ELCA_HR";
    public const string General = "ELCA_GENERAL";
    public const string CiiTowerSupport = "CII_TOWER_SUPPORT";
}
```

Valid payload values:

- `ELCA_HR`
- `ELCA_GENERAL`
- `CII_TOWER_SUPPORT`

Unexpected document/router classifier output defaults to `ELCA_GENERAL`.

## Intent values

```csharp
public enum QueryIntent
{
    Smalltalk,
    PolicyQuery,
    OutOfScope
}
```

Unexpected intent classifier output defaults to `PolicyQuery`.

## Qdrant payload schema

```json
{
  "source_file": "leave_policy.pdf",
  "page": 3,
  "chunk_index": 1,
  "file_type": "pdf",
  "agent": "ELCA_HR",
  "image_paths": ["/images/leave_policy/page3_img0.png"]
}
```

## Qdrant collection

| Field | Value |
|---|---|
| Name | `policy_docs` |
| Vector size | `1024` |
| Distance | `Cosine` |
| Minimum score | `0.45f` |

## SSE final sources event

Suggested final event:

```text
event: sources
data: [{"file":"leave_policy.pdf","page":3,"imagePaths":["/images/leave_policy/page3_img0.png"]}]

```
