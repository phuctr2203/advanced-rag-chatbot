# Hybrid Retrieval Comparison

Date: 2026-06-05

Scope: Phase 3 Enhancement 3.H6 verification. The corpus contained 177 Qdrant points at the time of the run.

Endpoint used: `POST /verify/hybrid-search`

Configuration:

```json
{
  "DenseWeight": 0.7,
  "KeywordWeight": 0.3,
  "ExactMatchKeywordWeight": 0.5,
  "Limit": 6,
  "CandidateLimit": 20,
  "MinimumScore": 0.45
}
```

## Result Summary

| Case | Query | Dense top result | Keyword top result | Hybrid top result | Outcome |
|---|---|---|---|---|---|
| Semantic | How many annual leave days do employees get with seniority? | Attribution of additional annual leave days with seniority.pdf, page 3 | Attribution of additional annual leave days with seniority.pdf, page 1 | Attribution of additional annual leave days with seniority.pdf, page 3 | Hybrid preserved the strongest semantic result while keeping keyword-supported policy pages in candidates. |
| Policy code | SCH-HR-003 | No dense result above threshold | Attribution of additional annual leave days with seniority.pdf, page 1 | Attribution of additional annual leave days with seniority.pdf, page 1 | Hybrid recovered an exact policy-code query that dense search missed. |
| Form name | Payment Request Form | Payment request form.docx, page 1 | Payment request form.docx, page 1 | Payment request form.docx, page 1 | All strategies found the form; hybrid kept the downloadable DOCX template at the top. |
| Vietnamese form title | GIẤY ĐỀ NGHỊ THANH TOÁN | Payment request form.docx, page 1 | Payment process V2_2023.pdf, page 6 | Payment request form.docx, page 1 | Hybrid favored the downloadable form template while keyword found the related payment-process PDF. |
| Acronym | PCCC equipment | SỔ TAY KHẨN CẤP - CII TOWER (final).pdf, page 17 | SỔ TAY KHẨN CẤP - CII TOWER (final).pdf, page 2 | SỔ TAY KHẨN CẤP - CII TOWER (final).pdf, page 17 | Hybrid kept the semantically strongest CII Tower fire-safety result and keyword confirmed the acronym/document family. |
| Article reference | Article 4 annual leave | Attribution of additional annual leave days with seniority.pdf, page 2 | Attribution of additional annual leave days with seniority.pdf, page 2 | Attribution of additional annual leave days with seniority.pdf, page 2 | All strategies agreed on the relevant annual-leave policy page. |

## Observations

- Dense search is strong for natural-language semantic questions.
- Keyword search is necessary for exact identifiers. `SCH-HR-003` returned no dense results above threshold, but keyword and hybrid found the correct policy.
- Keyword search is useful for bilingual form titles and acronyms, but standalone keyword scoring can be less precise than dense ranking for broad terms.
- Hybrid search gives the best default behavior for this corpus because it preserves semantic recall and adds exact-match recovery.
- The current bge-m3 integration returns dense TEI embeddings only. Sparse-vector support is not exposed through the current provider contract, so the pragmatic keyword service remains the Phase 3 hybrid implementation.

## Decision

Use `Hybrid` as the default retrieval mode for Phase 3.

Keep dense-only and keyword-only result views available for diagnostics and later evaluation, but use hybrid retrieval for the application runtime. Revisit native Qdrant sparse-vector hybrid search if the embedding service exposes bge-m3 sparse lexical vectors.
