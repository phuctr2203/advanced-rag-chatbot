#!/usr/bin/env python3
"""Run the full RAG evaluation pipeline from an input XLSX to an output XLSX."""

from __future__ import annotations

import argparse
import subprocess
import sys
from datetime import datetime
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_CONFIG = SCRIPT_DIR / "config.example.json"


def main() -> int:
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()
    config_path = Path(args.config).resolve()
    run_dir = Path(args.run_dir).resolve() if args.run_dir else default_run_dir(input_path)
    generated_path = run_dir / "generated.jsonl"
    scores_path = run_dir / "ragas_scores.jsonl"

    if not input_path.exists():
        print(f"Input XLSX not found: {input_path}", file=sys.stderr)
        return 2

    run_dir.mkdir(parents=True, exist_ok=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    common_python = [sys.executable]
    batch_command = [
        *common_python,
        str(SCRIPT_DIR / "run_rag_batch.py"),
        "--dataset",
        str(input_path),
        "--config",
        str(config_path),
        "--api-base",
        args.api_base,
        "--output-dir",
        str(run_dir),
        "--top-k",
        str(args.top_k),
    ]
    if args.limit is not None:
        batch_command += ["--limit", str(args.limit)]
    if args.include_prompt:
        batch_command.append("--include-prompt")
    if args.no_resume:
        batch_command.append("--no-resume")

    score_command = [
        *common_python,
        str(SCRIPT_DIR / "run_ragas_eval.py"),
        "--input",
        str(generated_path),
        "--output",
        str(scores_path),
        "--config",
        str(config_path),
    ]
    if args.limit is not None:
        score_command += ["--limit", str(args.limit)]
    if args.no_resume:
        score_command.append("--no-resume")

    summary_command = [
        *common_python,
        str(SCRIPT_DIR / "summarize_results.py"),
        "--input",
        str(scores_path),
        "--output-dir",
        str(run_dir),
        "--workbook",
        str(output_path),
    ]

    print(f"Input XLSX: {input_path}")
    print(f"Output XLSX: {output_path}")
    print(f"Run directory: {run_dir}")

    for label, command in [
        ("Batch generation", batch_command),
        ("RAGAS scoring", score_command),
        ("Workbook summary", summary_command),
    ]:
        print()
        print(f"== {label} ==")
        result = subprocess.run(command, cwd=REPO_ROOT, check=False)
        if result.returncode != 0:
            print(f"{label} failed with exit code {result.returncode}.", file=sys.stderr)
            return result.returncode

    print()
    print(f"Evaluation workbook written to {output_path}")
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run batch generation, RAGAS scoring, and Excel summary creation."
    )
    parser.add_argument("input", help="Input evaluation dataset XLSX.")
    parser.add_argument("output", help="Output evaluation_result XLSX.")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG), help="Evaluation config JSON.")
    parser.add_argument("--api-base", default="http://localhost:5000", help="Policy bot API base URL.")
    parser.add_argument("--top-k", type=int, default=6, help="Number of retrieved chunks to request.")
    parser.add_argument("--limit", type=int, help="Only evaluate the first N dataset rows.")
    parser.add_argument("--run-dir", help="Directory for generated JSONL, CSV, and summary artifacts.")
    parser.add_argument("--include-prompt", action="store_true", help="Include prompts in generated.jsonl.")
    parser.add_argument("--no-resume", action="store_true", help="Do not reuse existing generated.jsonl rows.")
    return parser.parse_args()


def default_run_dir(input_path: Path) -> Path:
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    stem = input_path.stem.replace(" ", "_")
    return SCRIPT_DIR / "outputs" / f"{stem}-{stamp}"


if __name__ == "__main__":
    raise SystemExit(main())
