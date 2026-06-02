# LLM Providers Reference

## Goal

Support company OpenWebUI and local Ollama through one OpenAI-compatible provider.

## Interface

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens = 1000, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

## Configuration

```json
{
  "LlmProvider": {
    "Active": "OpenWebUI",
    "OpenWebUI": {
      "BaseUrl": "https://your-company-openwebui.com",
      "ApiKey": "your-key",
      "Model": "llama33-70b"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.3"
    }
  }
}
```

## OpenAI-compatible chat completion

Endpoint:

```text
POST {BaseUrl}/v1/chat/completions
```

Non-streaming request:

```json
{
  "model": "llama33-70b",
  "messages": [
    { "role": "user", "content": "..." }
  ],
  "max_tokens": 1000,
  "stream": false
}
```

Streaming request:

```json
{
  "model": "llama33-70b",
  "messages": [
    { "role": "user", "content": "..." }
  ],
  "stream": true
}
```

## Classifier calls

Use non-streaming completion.

Rules:

- `maxTokens: 10`
- Trim whitespace.
- Validate exact allowed values.
- Default on unexpected value.

## RAG answer calls

Use streaming completion.

Rules:

- Stream token deltas as `IAsyncEnumerable<string>`.
- Preserve source line in accumulated answer for parser.
- Frontend can hide `SOURCES:` line if final sources event is used.

## Provider switching

Active provider selection:

```text
LlmProvider:Active = OpenWebUI | Ollama
```

Switching provider must not require code changes.

## Security

- Do not commit real API key.
- Prefer user secrets or environment variable override for real key.
- Never log Authorization header.

## Fallback note

If company OpenWebUI does not support streaming, implement provider fallback that reads non-streaming response and yields answer chunks. Document limitation in README.
