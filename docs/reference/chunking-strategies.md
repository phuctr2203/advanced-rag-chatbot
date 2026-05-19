# Chunking Strategies Reference

Chunking quality controls retrieval quality. Implement all three strategies, evaluate on real documents, then choose default.

## Shared settings

| Setting | Default |
|---|---:|
| Chunk size | 400 words |
| Overlap | 80 words |
| Minimum chunk size | 30 words |

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

## Strategy A — FixedSize

Best for quick baseline.

Algorithm:

1. Split page text into words.
2. Take 400 words.
3. Move start pointer by 320 words.
4. Drop chunk if below 30 words.
5. Preserve page metadata and image paths.

Pros:

- Easy to implement.
- Predictable chunk sizes.
- Works for messy text.

Cons:

- Cuts sentences and policy clauses mid-thought.
- Can split answer from its condition/exception.

## Strategy B — ParagraphBoundary

Best default candidate for policy documents.

Algorithm:

1. Split text by blank lines.
2. Detect heading-like lines.
3. Accumulate paragraphs until near 400 words.
4. If a paragraph exceeds 400 words, split it with Strategy A.
5. Use overlap from previous paragraph or last 80 words.

Pros:

- Keeps policy sections coherent.
- Better citation context.
- Still robust for long paragraphs.

Cons:

- Depends on parser preserving paragraph breaks.
- Chunk sizes can vary.

## Strategy C — SentenceWindow

Best when paragraph structure is poor but sentence punctuation is preserved.

Algorithm:

1. Split into sentences on `.`, `?`, `!`, `。`.
2. Accumulate sentences up to 400 words.
3. Carry last 1–2 sentences into next chunk.
4. Drop tiny chunks.

Pros:

- Avoids mid-sentence cuts.
- Better for Q&A answer spans.

Cons:

- Hard for abbreviations and multilingual punctuation.
- Vietnamese/French/German punctuation may need tuning.

## Evaluation guide

Use same document and same questions for all strategies.

Questions:

- 2 broad policy questions.
- 2 specific condition/exception questions.
- 1 citation-sensitive question.

Score each retrieval from 1–5:

| Score | Meaning |
|---:|---|
| 1 | Wrong document or unusable chunk |
| 2 | Related but answer missing |
| 3 | Answer partly present |
| 4 | Answer present but context incomplete |
| 5 | Answer and surrounding conditions present |

Choose strategy with highest average and most reliable citations.

## Recommended default

Start with `ParagraphBoundary`. Fall back to `FixedSize` inside very long paragraphs. Use `SentenceWindow` if parsed documents lose paragraph breaks.
