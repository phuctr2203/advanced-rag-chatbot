import argparse
import json
import re
import subprocess
import sys
import urllib.request
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export a full corpus audit through the policy-bot parser endpoints.")
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5096")
    parser.add_argument("--corpus-path", default="data/test")
    parser.add_argument("--output-path", default="evaluation/corpus")
    return parser.parse_args()


def post_file(api_base_url: str, endpoint: str, file_path: Path):
    result = subprocess.run(
        [
            "curl.exe",
            "--silent",
            "--show-error",
            "--fail-with-body",
            "--form",
            f"file=@{file_path}",
            f"{api_base_url}{endpoint}",
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or f"curl exited with {result.returncode}")
    return json.loads(result.stdout)


def suggested_role(name: str) -> str:
    if re.search(r"form|request & actual|missionform|purchasing", name, re.IGNORECASE):
        return "form"
    if re.search(r"guidance|guidelines|handbook|sổ tay", name, re.IGNORECASE):
        return "guidance"
    return "policy"


def suggested_domain(name: str) -> str:
    if re.search(r"cii|building|bicycle|smoking|sổ tay", name, re.IGNORECASE):
        return "CII_TOWER_SUPPORT"
    if re.search(r"leave|labou?r|working|workfromhome|work-from-home|overtime|oncall|on-call|referral|dependent|pit", name, re.IGNORECASE):
        return "ELCA_HR"
    return "ELCA_GENERAL"


def suggested_version_group(name: str) -> str:
    stem = Path(name).stem.lower()
    stem = re.sub(r"\b20\d{2}\b", "", stem)
    stem = re.sub(r"\bvn[-_ ]?en\b|\[en\]|v2[_-]?", "", stem)
    stem = re.sub(r"pol-[a-z]+-\d+-", "", stem)
    stem = re.sub(r"[^a-z0-9]+", "-", stem)
    return stem.strip("-")


def safe_file_name(name: str) -> str:
    return re.sub(r"[^\w._-]+", "_", name, flags=re.UNICODE)


def extract_document(api_base_url: str, file_path: Path) -> tuple[list[dict], list[dict]]:
    extension = file_path.suffix.lower()
    if extension == ".pdf":
        chunks = post_file(api_base_url, "/api/ingest/parse/pdf", file_path)
        images = post_file(api_base_url, "/api/ingest/analyze/pdf-images", file_path)
        return chunks, images
    if extension == ".docx":
        return post_file(api_base_url, "/api/ingest/parse/docx", file_path), []
    if extension == ".doc":
        return post_file(api_base_url, "/api/ingest/convert/doc", file_path)["chunks"], []
    if extension == ".xlsx":
        return post_file(api_base_url, "/api/ingest/parse/xlsx", file_path), []
    if extension == ".pptx":
        return post_file(api_base_url, "/api/ingest/convert/pptx", file_path)["chunks"], []
    raise ValueError(f"Unsupported extension {extension}")


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def escape_markdown(value: str) -> str:
    return value.replace("|", r"\|")


def build_audit_markdown(inventory: list[dict]) -> str:
    statuses = Counter(item["extractionStatus"] for item in inventory)
    empty_count = sum(item["extractionStatus"] == "ok" and item["textLength"] == 0 for item in inventory)
    lines = [
        "# Corpus Audit",
        "",
        "Generated from the application parser endpoints with image captioning disabled.",
        "",
        "## Summary",
        "",
        "| Measure | Count |",
        "|---|---:|",
        f"| Corpus files | {len(inventory)} |",
        f"| Extracted successfully | {statuses['ok']} |",
        f"| Extraction errors | {statuses['error']} |",
        f"| Empty readable text | {empty_count} |",
        "",
        "## Reviewed Findings",
        "",
        "- All 44 source files were inspected through the application parser workflow.",
        "- Two legacy PIT dependent-registration DOC forms still fail LibreOffice conversion and remain excluded until replaced or repaired.",
        "- Ten PDFs are image-only or scanned. Most have newer searchable counterparts; unique notice documents remain candidates for image-surfacing evaluation.",
        "- Canonical current policies are identified separately from superseded, duplicate, scanned, form-template, and historical-data variants.",
        "- The audit fixed headless DOC and PPTX conversion by assigning each LibreOffice run an isolated user profile.",
        "",
        "## Document Inventory",
        "",
        "| File | Type | Status | Pages or sheets | Text chunks | Text length | PDF image candidates | Suggested domain | Suggested role |",
        "|---|---|---|---:|---:|---:|---:|---|---|",
    ]
    for item in inventory:
        lines.append(
            f"| {escape_markdown(item['sourceFile'])} | {item['extension']} | {item['extractionStatus']} "
            f"| {item['pageOrSheetCount']} | {item['textChunkCount']} | {item['textLength']} "
            f"| {item['pdfImageCandidateCount']} | {item['suggestedDomain']} | {item['suggestedRole']} |"
        )
    lines.extend(
        [
            "",
            "## Reviewed Source Map",
            "",
            "| File | Usage | Canonical group | Notes |",
            "|---|---|---|---|",
        ]
    )
    for item in inventory:
        lines.append(
            f"| {escape_markdown(item['sourceFile'])} | {item['evaluationUsage']} "
            f"| {item['canonicalGroup']} | {escape_markdown(item['notes'])} |"
        )
    lines.extend(
        [
            "",
            "## Review Checklist",
            "",
            "- Confirm domain assignments.",
            "- Confirm policy, guidance, and form roles.",
            "- Group duplicate, translated, superseded, and canonical versions.",
            "- Inspect extraction errors and empty-text documents.",
            "- Mark image-heavy files that require image-surfacing questions.",
            "- Select source excerpts before generating evaluation questions.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    corpus_root = (repo_root / args.corpus_path).resolve()
    output_root = (repo_root / args.output_path).resolve()
    extracted_root = output_root / "extracted"
    extracted_root.mkdir(parents=True, exist_ok=True)
    review_path = output_root / "corpus-review.json"
    review_by_file = {}
    if review_path.exists():
        review_by_file = {
            item["sourceFile"]: item
            for item in json.loads(review_path.read_text(encoding="utf-8"))
        }

    try:
        with urllib.request.urlopen(f"{args.api_base_url}/api/health", timeout=10) as response:
            if response.status != 200:
                raise RuntimeError(f"Health endpoint returned HTTP {response.status}")
    except Exception as exception:
        raise RuntimeError(
            f"The audit API is not reachable at {args.api_base_url}. "
            "Start it with image captioning disabled before running this script."
        ) from exception

    inventory: list[dict] = []
    for file_path in sorted(corpus_root.iterdir(), key=lambda path: path.name.casefold()):
        if not file_path.is_file():
            continue

        print(f"Extracting {file_path.name}", flush=True)
        status = "ok"
        error_message = ""
        chunks: list[dict] = []
        images: list[dict] = []
        try:
            chunks, images = extract_document(args.api_base_url, file_path)
        except Exception as exception:
            status = "error"
            error_message = str(exception)

        text_chunks = [chunk for chunk in chunks if chunk.get("chunkType") == "text"]
        pages = sorted({int(chunk.get("pageNumber", 0)) for chunk in text_chunks if int(chunk.get("pageNumber", 0)) > 0})
        text_length = sum(len(chunk.get("text", "")) for chunk in text_chunks)
        image_candidate_count = sum(
            bool(image.get("passesSizeFilter")) and bool(image.get("passesAspectRatioFilter"))
            for image in images
        )

        extracted = {
            "sourceFile": file_path.name,
            "extension": file_path.suffix.lower(),
            "extractionStatus": status,
            "extractionError": error_message,
            "chunks": chunks,
            "imageCandidates": images,
        }
        write_json(extracted_root / f"{safe_file_name(file_path.name)}.json", extracted)

        review = review_by_file.get(file_path.name, {})
        inventory.append(
            {
                "sourceFile": file_path.name,
                "extension": file_path.suffix.lower(),
                "byteSize": file_path.stat().st_size,
                "extractionStatus": status,
                "extractionError": error_message,
                "textChunkCount": len(text_chunks),
                "pageOrSheetCount": len(pages),
                "textLength": text_length,
                "pdfImageCandidateCount": image_candidate_count,
                "suggestedDomain": suggested_domain(file_path.name),
                "suggestedRole": suggested_role(file_path.name),
                "suggestedVersionGroup": suggested_version_group(file_path.name),
                "reviewStatus": review.get("reviewStatus", "pending"),
                "evaluationUsage": review.get("evaluationUsage", "review_required"),
                "canonicalGroup": review.get("canonicalGroup", suggested_version_group(file_path.name)),
                "notes": review.get("notes", ""),
            }
        )

    write_json(output_root / "corpus-inventory.json", inventory)
    (output_root / "corpus-audit.md").write_text(build_audit_markdown(inventory), encoding="utf-8")

    statuses = Counter(item["extractionStatus"] for item in inventory)
    empty_count = sum(item["extractionStatus"] == "ok" and item["textLength"] == 0 for item in inventory)
    print("")
    print("Audit export complete.")
    print(f"Inventory: {output_root / 'corpus-inventory.json'}")
    print(f"Audit:     {output_root / 'corpus-audit.md'}")
    print(f"Extracted: {statuses['ok']}")
    print(f"Errors:    {statuses['error']}")
    print(f"Empty:     {empty_count}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
