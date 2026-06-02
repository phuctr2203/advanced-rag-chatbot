import json
import shutil
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.review.json"
FINAL = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.json"


def main() -> int:
    rows = json.loads(REVIEW.read_text(encoding="utf-8"))
    errors = []
    seen_questions = set()

    for row in rows:
        question_key = row["question"].strip().casefold()
        if question_key in seen_questions:
            errors.append(f"{row['id']}: duplicate question")
        seen_questions.add(question_key)

        if row["reviewStatus"] != "reviewed":
            errors.append(f"{row['id']}: cannot promote status {row['reviewStatus']}")
        if not row["referenceAnswer"].strip():
            errors.append(f"{row['id']}: missing reference answer")
        if row["scenario"] == "form_template_surfacing" and not row.get("expectedTemplateSource"):
            errors.append(f"{row['id']}: missing expectedTemplateSource")
        if row["scenario"] == "image_surfacing" and not row.get("expectedImagePath"):
            errors.append(f"{row['id']}: missing expectedImagePath")

    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1

    shutil.copyfile(REVIEW, FINAL)
    print(f"Promoted {len(rows)} reviewed rows to {FINAL}")
    print("Scenarios:", dict(Counter(row["scenario"] for row in rows)))
    print("Languages:", dict(Counter(row["language"] for row in rows)))
    print("Domains:", dict(Counter(row["domain"] for row in rows)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
