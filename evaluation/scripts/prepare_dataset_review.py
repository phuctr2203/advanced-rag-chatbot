import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[2]
DRAFT = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.draft.json"
REVIEW = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.review.json"
REPORT = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.review-report.md"
FORM_REGISTRY = ROOT / "data" / "form-registry.json"
IMAGE_CAPTIONS = ROOT / "evaluation" / "corpus" / "image-caption-review.json"


def compact(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def excerpt_answer(row: dict) -> str:
    text = compact(row["referenceAnswer"])
    keywords = row.get("draftMatchedKeywords", [])
    lowered = text.casefold()
    positions = [lowered.find(keyword.casefold()) for keyword in keywords if lowered.find(keyword.casefold()) >= 0]
    start = max(0, min(positions) - 120) if positions else 0
    answer = text[start : start + 320]
    if start > 0:
        answer = "..." + answer
    if start + 320 < len(text):
        answer += "..."
    return answer


def fixed_form_answer(language: str, source_file: str, download_available: bool) -> str:
    if language == "vi":
        return f"Biểu mẫu liên quan là {source_file}." + (" Biểu mẫu này có thể tải xuống." if download_available else "")
    if language == "fr":
        return f"Le formulaire correspondant est {source_file}." + (" Ce formulaire est disponible au téléchargement." if download_available else "")
    if language == "de":
        return f"Das passende Formular ist {source_file}." + (" Dieses Formular kann heruntergeladen werden." if download_available else "")
    return f"The relevant template is {source_file}." + (" This form is available to download." if download_available else "")


def registry_downloads() -> set[str]:
    entries = json.loads(FORM_REGISTRY.read_text(encoding="utf-8"))
    return {
        Path(entry.get("source_file", entry.get("docx_file", ""))).name
        for entry in entries
        if entry.get("download_path")
    }


def verified_image_captions() -> dict[tuple[str, int], dict]:
    entries = json.loads(IMAGE_CAPTIONS.read_text(encoding="utf-8"))
    return {
        (entry["sourceFile"], int(entry["page"])): entry
        for entry in entries
        if entry["reviewStatus"] == "verified"
    }


def main() -> int:
    rows = json.loads(DRAFT.read_text(encoding="utf-8"))
    downloads = registry_downloads()
    image_captions = verified_image_captions()
    pending = []
    reviewed = []

    for row in rows:
        item = dict(row)
        item["sourceExcerpts"] = item.pop("referenceContexts")
        item["reviewNotes"] = []
        item["reviewStatus"] = "reviewed"

        if item["scenario"] in {"grounded_text_policy", "mixed_intent"}:
            item["referenceAnswer"] = excerpt_answer(item)
            item["reviewNotes"].append("Concise source-backed answer candidate prepared from the selected excerpt.")

        elif item["scenario"] == "form_template_surfacing":
            source_file = item["expectedFormDownload"]
            download_available = Path(source_file).name in downloads
            item["expectedTemplateSource"] = source_file
            item["expectedFormDownload"] = source_file if download_available else None
            item["referenceAnswer"] = fixed_form_answer(item["language"], source_file, download_available)
            if download_available:
                item["reviewNotes"].append("Download expectation verified against data/form-registry.json.")
            else:
                item["reviewNotes"].append("Template retrieval only: no configured downloadable registry entry exists.")

        elif item["scenario"] == "image_surfacing":
            source_file = item["expectedSources"][0]["file"]
            page = int(item["expectedSources"][0]["page"])
            verified_caption = image_captions.get((source_file, page))
            if verified_caption is not None:
                item["referenceAnswer"] = verified_caption["caption"]
                item["sourceExcerpts"] = [verified_caption["caption"]]
                item["expectedImagePath"] = verified_caption["imagePath"]
                item["reviewNotes"].append("Image caption verified through the application parser with image captioning enabled.")
            elif not item["sourceExcerpts"]:
                item["reviewStatus"] = "pending_image_caption_verification"
                item["reviewNotes"].append("Image-only document is not currently indexed; ingest with image captioning and verify caption before acceptance.")
                pending.append(item["id"])
            else:
                item["reviewNotes"].append("Image source has an extracted text context or existing image-caption metadata.")

        elif item["scenario"] in {"smalltalk", "out_of_scope"}:
            item["reviewNotes"].append("Control row: retrieval and citations must be bypassed.")

        reviewed.append(item)

    REVIEW.write_text(json.dumps(reviewed, ensure_ascii=False, indent=2), encoding="utf-8")

    report = [
        "# RAG Evaluation Dataset Review",
        "",
        "EVAL 3 prepares an acceptance candidate from the validated EVAL 2 draft.",
        "",
        "## Summary",
        "",
        f"- Total rows: `{len(reviewed)}`",
        f"- Reviewed candidates: `{sum(row['reviewStatus'] == 'reviewed' for row in reviewed)}`",
        f"- Pending image-caption verification: `{len(pending)}`",
        f"- Configured downloadable forms: `{len(downloads)}`",
        "",
        "## Form Download Review",
        "",
        "Only files backed by a non-empty `download_path` in `data/form-registry.json` remain download expectations.",
        "Other form rows remain useful template-retrieval checks.",
        "",
        "## Pending Rows",
        "",
        *([f"- `{row_id}`" for row_id in pending] or ["- None"]),
        "",
        "## Acceptance Rule",
        "",
        (
            "Do not copy this file to `rag-evaluation-v1.json` until pending image-caption rows are verified and concise answer candidates are reviewed."
            if pending
            else "All rows satisfy the automated acceptance checks and may be promoted to `rag-evaluation-v1.json`."
        ),
        "",
    ]
    REPORT.write_text("\n".join(report), encoding="utf-8")

    print(f"Wrote review candidate: {REVIEW}")
    print(f"Wrote review report:    {REPORT}")
    print(f"Rows: {len(reviewed)}")
    print(f"Reviewed candidates: {sum(row['reviewStatus'] == 'reviewed' for row in reviewed)}")
    print(f"Pending image captions: {len(pending)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
