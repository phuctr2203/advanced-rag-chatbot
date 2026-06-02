import argparse
import asyncio
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

from ragas_adapter import create_metrics, score_result

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DATASET = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.json"
DEFAULT_INVENTORY = ROOT / "evaluation" / "corpus" / "corpus-inventory.json"
DEFAULT_CONFIG = ROOT / "evaluation" / "config" / "evaluation.local.json"
EXAMPLE_CONFIG = ROOT / "evaluation" / "config" / "evaluation.example.json"
RESULTS_ROOT = ROOT / "evaluation" / "results"
RAGAS_SCENARIOS = {"grounded_text_policy", "form_template_surfacing", "image_surfacing", "mixed_intent"}


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(f"{path.suffix}.tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def request_json(method: str, url: str, timeout: int, payload=None, retries: int = 0):
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json"} if data is not None else {}
    for attempt in range(retries + 1):
        try:
            request = urllib.request.Request(url, data=data, headers=headers, method=method)
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return json.load(response)
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            if attempt >= retries:
                raise
            time.sleep(min(2**attempt, 5))


def create_run_directory(run_directory: str | None, run_name: str | None) -> Path:
    if run_directory:
        result = Path(run_directory).resolve()
    else:
        timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H%M%SZ")
        result = RESULTS_ROOT / (run_name or f"{timestamp}-baseline")
    result.mkdir(parents=True, exist_ok=True)
    return result


def git_commit() -> str:
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, text=True, stderr=subprocess.DEVNULL
        ).strip()
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def load_config(path: str | None) -> tuple[Path, dict]:
    config_path = Path(path).resolve() if path else DEFAULT_CONFIG
    if not config_path.exists():
        config_path = EXAMPLE_CONFIG
    return config_path, read_json(config_path)


def expected_files(inventory_path: Path) -> tuple[set[str], set[str]]:
    rows = read_json(inventory_path)
    excluded = {
        row["sourceFile"]
        for row in rows
        if row.get("evaluationUsage", "").startswith("parser_error_")
    }
    return {row["sourceFile"] for row in rows} - excluded, excluded


def indexed_files(config: dict) -> set[str]:
    base_url = config["qdrantBaseUrl"].rstrip("/")
    collection = urllib.parse.quote(config["qdrantCollection"], safe="")
    url = f"{base_url}/collections/{collection}/points/scroll"
    offset = None
    files = set()
    while True:
        payload = {"limit": 256, "with_payload": True, "with_vector": False}
        if offset is not None:
            payload["offset"] = offset
        response = request_json("POST", url, config["requestTimeoutSeconds"], payload, config["retryCount"])
        result = response["result"]
        for point in result.get("points", []):
            source_file = point.get("payload", {}).get("source_file")
            if source_file:
                files.add(source_file)
        offset = result.get("next_page_offset")
        if offset is None:
            return files


def run_preflight(config: dict, inventory_path: Path) -> dict:
    expected, excluded = expected_files(inventory_path)
    indexed = indexed_files(config)
    missing = sorted(expected - indexed, key=str.casefold)
    result = {
        "expectedFileCount": len(expected),
        "indexedFileCount": len(indexed),
        "excludedFiles": sorted(excluded, key=str.casefold),
        "missingFiles": missing,
        "unexpectedFiles": sorted(indexed - expected, key=str.casefold),
    }
    if missing:
        raise RuntimeError("Qdrant corpus preflight failed. Missing indexed files:\n- " + "\n- ".join(missing))
    return result


def filter_rows(rows: list[dict], limit: int | None, ids: set[str]) -> list[dict]:
    selected = [row for row in rows if not ids or row["id"] in ids]
    return selected[:limit] if limit else selected


def deterministic_checks(row: dict, result: dict) -> dict:
    expected_sources = row.get("expectedSources", [])
    actual_sources = result.get("sources", [])
    source_names = {source.get("file", "") for source in actual_sources}
    image_paths = {
        context.get("imagePath", "") for context in result.get("retrievedContexts", []) if context.get("imagePath")
    }
    actual_downloads = result.get("formDownloads", [])
    expected_download = row.get("expectedFormDownload")
    no_sources_expected = row["expectedIntent"] != "POLICY_QUERY" or result.get("noAnswer", False)
    filename_ok = not expected_sources or any(source["file"] in source_names for source in expected_sources)
    page_ok = not expected_sources or any(
        source.get("file") == expected["file"] and abs(source.get("page", 0) - expected["page"]) <= 1
        for source in actual_sources
        for expected in expected_sources
    )
    return {
        "intent": result.get("intent") == row["expectedIntent"],
        "languageDetection": result.get("language") == row["language"],
        "retrievalBypass": row["expectedIntent"] == "POLICY_QUERY" or not result.get("retrievalRan", False),
        "citationFilename": filename_ok,
        "citationPage": page_ok,
        "noMisleadingCitation": not actual_sources if no_sources_expected else True,
        "formDownload": download_matches(expected_download, actual_downloads) if expected_download else not actual_downloads,
        "imageSurfacing": (bool(image_paths) if row.get("expectedImage") else True),
    }


def download_matches(expected_download: str, actual_downloads: list[dict]) -> bool:
    expected_stem = Path(expected_download).stem.casefold()
    return any(
        expected_stem == Path(download.get("downloadPath", "")).stem.casefold()
        or expected_stem == download.get("formName", "").casefold()
        for download in actual_downloads
    )


async def collect_results(config: dict, rows: list[dict], output_path: Path) -> list[dict]:
    existing = {row["id"]: row for row in read_json(output_path)} if output_path.exists() else {}
    semaphore = asyncio.Semaphore(max(1, config["concurrency"]))
    lock = asyncio.Lock()

    async def collect(row: dict) -> None:
        if row["id"] in existing and existing[row["id"]].get("status") == "completed":
            return
        async with semaphore:
            try:
                endpoint = f"{config['policyBotBaseUrl'].rstrip('/')}/api/evaluation/query"
                result = await asyncio.to_thread(
                    request_json,
                    "POST",
                    endpoint,
                    config["requestTimeoutSeconds"],
                    {"question": row["question"]},
                    config["retryCount"],
                )
                existing[row["id"]] = {
                    "id": row["id"],
                    "status": "completed",
                    "result": result,
                    "checks": deterministic_checks(row, result),
                }
            except Exception as exc:
                existing[row["id"]] = {"id": row["id"], "status": "failed", "error": str(exc)}
            async with lock:
                write_json(output_path, sorted(existing.values(), key=lambda item: item["id"]))
                print(f"Collected {row['id']}: {existing[row['id']]['status']}")

    await asyncio.gather(*(collect(row) for row in rows))
    return sorted(existing.values(), key=lambda item: item["id"])


async def score_profile(config: dict, profile_name: str, rows: list[dict], raw_results: list[dict], output_path: Path):
    profile = config["profiles"][profile_name]
    metrics = create_metrics(profile, config["teiBaseUrl"], config["scoreTimeoutSeconds"])
    raw_by_id = {row["id"]: row for row in raw_results}
    existing = {row["id"]: row for row in read_json(output_path)} if output_path.exists() else {}
    for row in rows:
        if row["id"] in existing and existing[row["id"]].get("status") in {"completed", "skipped"}:
            continue
        raw = raw_by_id.get(row["id"], {})
        result = raw.get("result", {})
        if row["scenario"] not in RAGAS_SCENARIOS or raw.get("status") != "completed" or result.get("noAnswer"):
            existing[row["id"]] = {"id": row["id"], "status": "skipped"}
        else:
            try:
                scores = await score_with_retries(config, metrics, row, result)
                existing[row["id"]] = {"id": row["id"], "status": "completed", "scores": scores}
            except Exception as exc:
                existing[row["id"]] = {"id": row["id"], "status": "failed", "error": str(exc)}
        write_json(output_path, sorted(existing.values(), key=lambda item: item["id"]))
        print(f"Scored {profile_name} {row['id']}: {existing[row['id']]['status']}")


async def score_with_retries(config: dict, metrics: dict, row: dict, result: dict) -> dict:
    for attempt in range(config["retryCount"] + 1):
        try:
            return await asyncio.wait_for(
                score_result(metrics, row, result), timeout=config["scoreTimeoutSeconds"]
            )
        except Exception:
            if attempt >= config["retryCount"]:
                raise
            await asyncio.sleep(min(2**attempt, 5))


def metadata(config_path: Path, dataset_path: Path, inventory_path: Path, args, preflight: dict | None) -> dict:
    return {
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "gitCommit": git_commit(),
        "configPath": str(config_path),
        "datasetPath": str(dataset_path),
        "inventoryPath": str(inventory_path),
        "action": args.action,
        "limit": args.limit,
        "profiles": args.profiles,
        "corpusPreflight": preflight,
    }


async def main_async(args) -> int:
    config_path, config = load_config(args.config)
    dataset_path = Path(args.dataset).resolve()
    inventory_path = Path(args.inventory).resolve()
    rows = filter_rows(read_json(dataset_path), args.limit, set(args.ids))
    preflight = run_preflight(config, inventory_path)
    run_directory = create_run_directory(args.run_directory, args.run_name)
    write_json(run_directory / "run-metadata.json", metadata(config_path, dataset_path, inventory_path, args, preflight))
    print(f"Qdrant preflight passed: {preflight['indexedFileCount']} indexed files")
    if args.action == "preflight":
        return 0

    raw_path = run_directory / "raw-results.json"
    raw_results = await collect_results(config, rows, raw_path)
    if args.action == "collect":
        return 0

    for profile_name in args.profiles:
        if profile_name not in config["profiles"]:
            raise RuntimeError(f"Unknown evaluator profile: {profile_name}")
        await score_profile(config, profile_name, rows, raw_results, run_directory / f"scores-{profile_name}.json")
    return 0


def parse_args():
    parser = argparse.ArgumentParser(description="Run resumable offline policy-bot RAG evaluation.")
    parser.add_argument("action", choices=["preflight", "collect", "run"])
    parser.add_argument("--config")
    parser.add_argument("--dataset", default=str(DEFAULT_DATASET))
    parser.add_argument("--inventory", default=str(DEFAULT_INVENTORY))
    parser.add_argument("--run-directory")
    parser.add_argument("--run-name")
    parser.add_argument("--limit", type=int)
    parser.add_argument("--ids", nargs="*", default=[])
    parser.add_argument("--profiles", nargs="+", default=["ollama-gpt-oss-120b", "openwebui-gpt-oss-120b"])
    return parser.parse_args()


if __name__ == "__main__":
    try:
        sys.exit(asyncio.run(main_async(parse_args())))
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
