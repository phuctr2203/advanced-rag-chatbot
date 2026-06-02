import json
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.review.json"


def main() -> int:
    rows = json.loads(REVIEW.read_text(encoding="utf-8"))
    errors = []
    warnings = []
    ids = set()

    for row in rows:
        row_id = row["id"]
        if row_id in ids:
            errors.append(f"{row_id}: duplicate id")
        ids.add(row_id)

        if not row.get("referenceAnswer", "").strip():
            errors.append(f"{row_id}: missing reference answer")
        if row["scenario"] in {"grounded_text_policy", "mixed_intent"} and not row.get("sourceExcerpts"):
            errors.append(f"{row_id}: missing source excerpts")
        if row["scenario"] == "form_template_surfacing" and not row.get("expectedTemplateSource"):
            errors.append(f"{row_id}: missing expected template source")
        if row["reviewStatus"] != "reviewed":
            warnings.append(f"{row_id}: {row['reviewStatus']}")

    statuses = Counter(row["reviewStatus"] for row in rows)
    print(f"Rows: {len(rows)}")
    print("Statuses:", dict(statuses))
    print(f"Errors: {len(errors)}")
    print(f"Warnings: {len(warnings)}")
    for error in errors:
        print(f"ERROR: {error}")
    for warning in warnings:
        print(f"WARNING: {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
