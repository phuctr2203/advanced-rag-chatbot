import json
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[2]
DATASET = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.draft.json"
EXTRACTED = ROOT / "evaluation" / "corpus" / "extracted"
SUMMARY = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.draft-summary.md"


def load_documents() -> dict[str, dict]:
    documents = {}
    for path in EXTRACTED.glob("*.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        documents[document["sourceFile"]] = document
    return documents


def table(counter: Counter) -> list[str]:
    return [f"| {key} | {value} |" for key, value in sorted(counter.items())]


def main() -> int:
    rows = json.loads(DATASET.read_text(encoding="utf-8"))
    documents = load_documents()
    errors: list[str] = []
    warnings: list[str] = []

    seen_ids = set()
    seen_questions = set()
    for row in rows:
        row_id = row["id"]
        question_key = row["question"].strip().casefold()
        if row_id in seen_ids:
            errors.append(f"Duplicate id: {row_id}")
        if question_key in seen_questions:
            errors.append(f"Duplicate question: {row['question']}")
        seen_ids.add(row_id)
        seen_questions.add(question_key)

        if not row["referenceAnswer"].strip():
            errors.append(f"{row_id}: empty reference answer")

        for source in row["expectedSources"]:
            file_name = source["file"]
            if file_name not in documents:
                errors.append(f"{row_id}: missing extracted source {file_name}")
                continue
            text_pages = {
                int(chunk.get("pageNumber", 0))
                for chunk in documents[file_name]["chunks"]
                if chunk.get("chunkType") == "text"
            }
            if row["scenario"] not in {"image_surfacing"} and int(source["page"]) not in text_pages:
                errors.append(f"{row_id}: source page {source['page']} has no extracted text in {file_name}")

        if row["scenario"] == "image_surfacing" and not row["expectedImage"]:
            errors.append(f"{row_id}: image row must set expectedImage")
        if row["scenario"] in {"smalltalk", "out_of_scope"} and row["expectedSources"]:
            errors.append(f"{row_id}: control row must not have sources")
        if row["scenario"] == "form_template_surfacing" and not row["expectedFormDownload"]:
            errors.append(f"{row_id}: form row must declare expectedFormDownload")
        if row["scenario"] in {"grounded_text_policy", "mixed_intent"} and not row.get("draftMatchedKeywords"):
            warnings.append(f"{row_id}: source excerpt was selected by fallback because no draft keyword matched")
        reference = row["referenceAnswer"].casefold()
        if row["scenario"] in {"grounded_text_policy", "mixed_intent"} and (
            "table of contents" in reference or "mục lục" in reference or row["referenceAnswer"].count("...") >= 3
        ):
            warnings.append(f"{row_id}: selected source excerpt appears to be a table-of-contents passage")
        if row["reviewStatus"] != "draft_requires_review":
            warnings.append(f"{row_id}: unexpected draft review status {row['reviewStatus']}")

    scenario_counts = Counter(row["scenario"] for row in rows)
    language_counts = Counter(row["language"] for row in rows)
    domain_counts = Counter(row["domain"] for row in rows)
    type_counts = Counter(row["questionType"] for row in rows)

    expected_scenarios = Counter(
        {
            "grounded_text_policy": 72,
            "form_template_surfacing": 12,
            "image_surfacing": 4,
            "smalltalk": 4,
            "out_of_scope": 4,
            "mixed_intent": 4,
        }
    )
    expected_languages = Counter({"en": 47, "vi": 27, "fr": 13, "de": 13})
    if len(rows) != 100:
        errors.append(f"Expected 100 rows, found {len(rows)}")
    if scenario_counts != expected_scenarios:
        errors.append(f"Scenario distribution mismatch: {dict(scenario_counts)}")
    if language_counts != expected_languages:
        errors.append(f"Language distribution mismatch: {dict(language_counts)}")
    if domain_counts["CII_TOWER_SUPPORT"] < 20:
        errors.append(f"CII_TOWER_SUPPORT coverage is too low: {domain_counts['CII_TOWER_SUPPORT']}")

    lines = [
        "# RAG Evaluation Draft Summary",
        "",
        "This file summarizes the generated EVAL 2 draft. Every row remains `draft_requires_review` until EVAL 3.",
        "",
        "## Validation",
        "",
        f"- Rows: `{len(rows)}`",
        f"- Errors: `{len(errors)}`",
        f"- Warnings: `{len(warnings)}`",
        "",
        "## Scenarios",
        "",
        "| Scenario | Count |",
        "|---|---:|",
        *table(scenario_counts),
        "",
        "## Languages",
        "",
        "| Language | Count |",
        "|---|---:|",
        *table(language_counts),
        "",
        "## Domains",
        "",
        "| Domain | Count |",
        "|---|---:|",
        *table(domain_counts),
        "",
        "## Question Types",
        "",
        "| Type | Count |",
        "|---|---:|",
        *table(type_counts),
        "",
        "## Errors",
        "",
        *([f"- {error}" for error in errors] or ["- None"]),
        "",
        "## Warnings",
        "",
        *([f"- {warning}" for warning in warnings] or ["- None"]),
        "",
        "## EVAL 3 Review Checklist",
        "",
        "- Rewrite extracted source excerpts into concise reference answers.",
        "- Confirm each selected page directly supports the question.",
        "- Check multilingual wording and answer-language expectations.",
        "- Confirm image-caption rows after ingestion with image captioning enabled.",
        "- Confirm form aliases and downloadable template mappings.",
        "- Remove ambiguous or redundant rows before marking the benchmark reviewed.",
        "",
    ]
    SUMMARY.write_text("\n".join(lines), encoding="utf-8")

    print(f"Rows: {len(rows)}")
    print("Scenarios:", dict(scenario_counts))
    print("Languages:", dict(language_counts))
    print("Domains:", dict(domain_counts))
    print(f"Errors: {len(errors)}")
    print(f"Warnings: {len(warnings)}")
    print(f"Summary: {SUMMARY}")
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
