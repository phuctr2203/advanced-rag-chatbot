# Phase 6 — Agent Orchestration & MCP

Goal: add domain-aware routing and expose policy operations as MCP tools.

Start only after Phase 5 is fully complete.

## Task 6.1 — Router agent

Implement `RouterAgentService`.

Prompt:

```text
Classify the following question into exactly one category:
- ELCA_HR
- ELCA_GENERAL
- CII_TOWER_SUPPORT
Respond with ONLY the category name.

Question: {userQuery}
```

Rules:

- Call only after intent is `POLICY_QUERY`.
- Use `maxTokens: 10`.
- Unexpected response defaults to `ELCA_GENERAL`.
- Log route for debugging.

## Task 6.2 — Agent-filtered vector search

Update query flow:

1. Detect language.
2. Classify intent.
3. If policy query, classify route agent.
4. Embed query.
5. Search Qdrant with `agent` payload filter.
6. Build prompt from filtered chunks.

Qdrant payload filter:

```json
{
  "must": [
    {
      "key": "agent",
      "match": { "value": "ELCA_HR" }
    }
  ]
}
```

Verification:

- Ask at least 2 HR questions.
- Ask at least 2 general IT/security/finance questions.
- Ask at least 2 CII Tower support questions.
- Confirm search only returns chunks from expected domain.

## Task 6.3 — Tool-calling loop

Test Llama 3.3 70B tool calling first.

Tools:

| Tool | Description |
|---|---|
| `search_policy` | Search policy documents by query and optional agent filter |
| `list_documents` | List ingested files with agent tags |
| `ingest_document` | Ingest document from server-side file path |

If tool calling is unreliable, keep prompt-based router and skip production tool loop.

## Task 6.4 — MCP server

Add `ModelContextProtocol` package.

Configure:

```csharp
builder.Services.AddMcpServer().WithHttpTransport();
```

Expose tools:

- `search_policy`
- `list_documents`
- `ingest_document`

Tool input requirements:

### search_policy

```json
{
  "query": "annual leave policy",
  "agent": "ELCA_HR"
}
```

`agent` optional. If absent, search all or route first depending final design.

### list_documents

```json
{}
```

Returns source files, agent tags, file types, chunk counts.

### ingest_document

```json
{
  "path": "C:\\Policies\\leave.pdf",
  "agent": "ELCA_HR"
}
```

`agent` optional. Validate path is server-accessible.

## Task 6.5 — External MCP test

Test tools from MCP-capable client.

Verify:

- Tool schema loads.
- `list_documents` returns current Qdrant metadata.
- `search_policy` returns chunks and source metadata.
- `ingest_document` ingests supported file.

## Done criteria

- Router correctly classifies domain for demo questions.
- Qdrant search uses agent filter.
- MCP tools can be called externally.
- MCP responses include source file and page metadata.
