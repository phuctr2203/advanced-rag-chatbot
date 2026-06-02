# RAG Evaluation Draft Summary

This file summarizes the generated EVAL 2 draft. Every row remains `draft_requires_review` until EVAL 3.

## Validation

- Rows: `100`
- Errors: `0`
- Warnings: `0`

## Scenarios

| Scenario | Count |
|---|---:|
| form_template_surfacing | 12 |
| grounded_text_policy | 72 |
| image_surfacing | 4 |
| mixed_intent | 4 |
| out_of_scope | 4 |
| smalltalk | 4 |

## Languages

| Language | Count |
|---|---:|
| de | 13 |
| en | 47 |
| fr | 13 |
| vi | 27 |

## Domains

| Domain | Count |
|---|---:|
| CII_TOWER_SUPPORT | 20 |
| CONTROL | 8 |
| ELCA_GENERAL | 28 |
| ELCA_HR | 44 |

## Question Types

| Type | Count |
|---|---:|
| comparison | 3 |
| direct_fact | 8 |
| eligibility | 7 |
| form_download | 12 |
| image_surface | 4 |
| mixed_intent | 4 |
| numeric_fact | 11 |
| out_of_scope | 4 |
| procedure | 29 |
| prohibition | 14 |
| smalltalk | 4 |

## Errors

- None

## Warnings

- None

## EVAL 3 Review Checklist

- Rewrite extracted source excerpts into concise reference answers.
- Confirm each selected page directly supports the question.
- Check multilingual wording and answer-language expectations.
- Confirm image-caption rows after ingestion with image captioning enabled.
- Confirm form aliases and downloadable template mappings.
- Remove ambiguous or redundant rows before marking the benchmark reviewed.
