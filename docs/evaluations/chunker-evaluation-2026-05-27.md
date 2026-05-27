# Chunker Evaluation

Date: 2026-05-27 10:56 +07:00

Documents:
- Employees Laptop Buy- Back Scheme.pdf
- How to Refer a Candidate on Oracle.docx
- Overtime Form.xlsx

Questions:
- What is the employee laptop buy-back scheme?
- What conditions or steps must employees follow for laptop buy-back?
- How can an employee refer a candidate on Oracle?
- What information is captured by the overtime form?
- Which source document should be cited for candidate referral steps?

## FixedSize

Collection: `policy_docs_fixed`
Chunks: 13
Average score: 4.00/5

| Question | Score | Top citation | Top score |
|---|---:|---|---:|
| What is the employee laptop buy-back scheme? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 3, chunk 2 | 0.681 |
| What conditions or steps must employees follow for laptop buy-back? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 3, chunk 2 | 0.773 |
| How can an employee refer a candidate on Oracle? | 4 | How to Refer a Candidate on Oracle.docx, page 1, chunk 6 | 0.496 |
| What information is captured by the overtime form? | 5 | Overtime Form.xlsx, page 1, chunk 7 | 0.736 |
| Which source document should be cited for candidate referral steps? | 1 | Employees Laptop Buy- Back Scheme.pdf, page 6, chunk 5 | 0.487 |

## ParagraphBoundary

Collection: `policy_docs_para`
Chunks: 15
Average score: 4.00/5

| Question | Score | Top citation | Top score |
|---|---:|---|---:|
| What is the employee laptop buy-back scheme? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 4, chunk 5 | 0.672 |
| What conditions or steps must employees follow for laptop buy-back? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 4, chunk 5 | 0.768 |
| How can an employee refer a candidate on Oracle? | 4 | How to Refer a Candidate on Oracle.docx, page 1, chunk 7 | 0.496 |
| What information is captured by the overtime form? | 5 | Overtime Form.xlsx, page 1, chunk 8 | 0.736 |
| Which source document should be cited for candidate referral steps? | 1 | Employees Laptop Buy- Back Scheme.pdf, page 6, chunk 6 | 0.487 |

## SentenceWindow

Collection: `policy_docs_sentence`
Chunks: 13
Average score: 4.00/5

| Question | Score | Top citation | Top score |
|---|---:|---|---:|
| What is the employee laptop buy-back scheme? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 3, chunk 2 | 0.676 |
| What conditions or steps must employees follow for laptop buy-back? | 5 | Employees Laptop Buy- Back Scheme.pdf, page 3, chunk 2 | 0.782 |
| How can an employee refer a candidate on Oracle? | 4 | How to Refer a Candidate on Oracle.docx, page 1, chunk 6 | 0.496 |
| What information is captured by the overtime form? | 5 | Overtime Form.xlsx, page 1, chunk 7 | 0.733 |
| Which source document should be cited for candidate referral steps? | 1 | Employees Laptop Buy- Back Scheme.pdf, page 6, chunk 5 | 0.486 |

## Decision

Selected strategy: **ParagraphBoundary**

Reason: highest average retrieval score (4.00/5) across the five representative questions. When scores tie, ParagraphBoundary is preferred because it keeps policy paragraphs and heading-adjacent context intact, which improves answer completeness and citation readability.

