#!/usr/bin/env python3
"""Score generated RAG rows with RAGAS."""

from __future__ import annotations

import argparse
import asyncio
import importlib
import json
import math
import os
import sys
import httpx
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_CONFIG = SCRIPT_DIR / "config.example.json"
SCORE_COLUMNS = [
    "context_precision",
    "context_recall",
    "context_relevance",
    "faithfulness",
    "answer_relevancy",
    "answer_correctness",
]


async def main_async() -> int:
    args = parse_args()
    config = load_json(args.config)
    input_path = Path(args.input)
    output_path = Path(args.output) if args.output else input_path.with_name("ragas_scores.jsonl")

    rows = read_jsonl(input_path)
    if args.limit is not None:
        rows = rows[: args.limit]

    completed_row_ids = read_completed_row_ids(output_path, retry_failed=args.retry_failed) if args.resume else set()
    pending_rows = [row for row in rows if str(row.get("row_id", "")) not in completed_row_ids]

    try:
        metric_runner = build_metric_runner(config)
    except Exception as exc:  # noqa: BLE001 - setup errors should be explicit for local environments.
        print(f"RAGAS setup failed: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2

    output_path.parent.mkdir(parents=True, exist_ok=True)
    mode = "a" if args.resume else "w"
    if completed_row_ids:
        print(f"Resuming from {output_path}: {len(completed_row_ids)} scored row(s) already present")

    with output_path.open(mode, encoding="utf-8") as writer:
        for index, row in enumerate(pending_rows, start=len(completed_row_ids) + 1):
            scored = dict(row)
            if row.get("error"):
                scored["ragas"] = {"error": row["error"], "scores": {}}
            else:
                scored["ragas"] = await metric_runner.score(row)

            writer.write(json.dumps(scored, ensure_ascii=False) + "\n")
            writer.flush()
            print(f"[{index}/{len(rows)}] row {row.get('row_id')}: scored")

    print(f"Scores written to {output_path}")
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run RAGAS metrics over generated evaluation rows.")
    parser.add_argument("--input", required=True, help="Path to generated.jsonl.")
    parser.add_argument("--output", help="Output JSONL path. Defaults to ragas_scores.jsonl beside input.")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG), help="Path to evaluation config JSON.")
    parser.add_argument("--limit", type=int, help="Only score first N rows.")
    parser.add_argument("--no-resume", dest="resume", action="store_false", help="Overwrite output and score from the beginning.")
    parser.add_argument("--retry-failed", action="store_true", help="When resuming, retry rows with failed or incomplete RAGAS scores.")
    parser.set_defaults(resume=True)
    return parser.parse_args()


def load_json(path: str | Path) -> dict:
    config_path = Path(path)
    if not config_path.exists():
        return {}

    return json.loads(config_path.read_text(encoding="utf-8"))


def read_jsonl(path: Path) -> list[dict]:
    rows = []
    with path.open("r", encoding="utf-8") as reader:
        for line in reader:
            if line.strip():
                rows.append(json.loads(line))
    return rows


def read_completed_row_ids(path: Path, retry_failed: bool = False) -> set[str]:
    completed = set()
    if not path.exists():
        return set()

    with path.open("r", encoding="utf-8") as reader:
        for line in reader:
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                continue

            row_id = str(record.get("row_id", "")).strip()
            if row_id and (not retry_failed or is_successfully_scored(record)):
                completed.add(row_id)

    return completed


def is_successfully_scored(record: dict) -> bool:
    ragas = record.get("ragas") or {}
    scores = ragas.get("scores") or {}
    errors = ragas.get("errors") or {}
    if errors or ragas.get("error"):
        return False

    for metric_name in SCORE_COLUMNS:
        if not is_valid_score(scores.get(metric_name)):
            return False

    return True


def is_valid_score(value: object) -> bool:
    if value is None:
        return False

    if isinstance(value, float) and math.isnan(value):
        return False

    return True


def build_metric_runner(config: dict) -> "RagasMetricRunner":
    ragas_llms = importlib.import_module("ragas.llms")
    langchain_openai = importlib.import_module("langchain_openai")

    evaluator_config = config.get("evaluator", {})
    os.environ["OPENAI_API_KEY"] = evaluator_config.get("apiKey", "ollama")
    verify_ssl = bool(evaluator_config.get("verifySsl", True))
    timeout = int(evaluator_config.get("timeoutSeconds", 180))
    chat_llm = langchain_openai.ChatOpenAI(
        model=evaluator_config.get("model", "gpt-oss:120b-cloud"),
        api_key=evaluator_config.get("apiKey", "ollama"),
        base_url=evaluator_config.get("baseUrl", "http://localhost:11434/v1"),
        timeout=timeout,
        http_client=httpx.Client(verify=verify_ssl, timeout=timeout),
        http_async_client=httpx.AsyncClient(verify=verify_ssl, timeout=timeout),
    )
    evaluator_llm = ragas_llms.LangchainLLMWrapper(chat_llm)

    embeddings = None
    embeddings_config = config.get("embeddings", {})
    if embeddings_config:
        try:
            ragas_embeddings = importlib.import_module("ragas.embeddings")
            embeddings_verify_ssl = bool(embeddings_config.get("verifySsl", True))
            embeddings_timeout = int(embeddings_config.get("timeoutSeconds", 180))
            langchain_embeddings = langchain_openai.OpenAIEmbeddings(
                model=embeddings_config.get("model", "BAAI/bge-m3"),
                base_url=embeddings_config.get("baseUrl", "http://localhost:8080/v1"),
                api_key=embeddings_config.get("apiKey", "tei"),
                tiktoken_enabled=False,
                check_embedding_ctx_length=False,
                http_client=httpx.Client(verify=embeddings_verify_ssl, timeout=embeddings_timeout),
                http_async_client=httpx.AsyncClient(verify=embeddings_verify_ssl, timeout=embeddings_timeout),
            )
            embeddings = ragas_embeddings.LangchainEmbeddingsWrapper(langchain_embeddings)
        except Exception as exc:  # noqa: BLE001 - answer relevancy will report this.
            embeddings = MetricSetupError(exc)

    return RagasMetricRunner(evaluator_llm, embeddings)


class MetricSetupError:
    def __init__(self, error: Exception):
        self.error = error


class RagasMetricRunner:
    def __init__(self, llm: object, embeddings: object):
        self.llm = llm
        self.embeddings = embeddings
        self.metrics = self._build_metrics()

    async def score(self, row: dict) -> dict:
        sample = build_sample(row)
        scores = {}
        errors = {}

        for metric_name, metric in self.metrics.items():
            if isinstance(metric, MetricSetupError):
                errors[metric_name] = f"{type(metric.error).__name__}: {metric.error}"
                scores[metric_name] = None
                continue

            try:
                scores[metric_name] = normalize_score(await metric.single_turn_ascore(sample))
            except Exception as exc:  # noqa: BLE001 - continue scoring other metrics.
                errors[metric_name] = f"{type(exc).__name__}: {exc}"
                scores[metric_name] = None

        return {
            "scores": scores,
            "errors": errors,
        }

    def _build_metrics(self) -> dict:
        metrics = {}
        try:
            collections = importlib.import_module("ragas.metrics.collections")
        except ModuleNotFoundError:
            collections = importlib.import_module("ragas.metrics")

        metrics["context_precision"] = instantiate_metric(
            collections,
            ["ContextPrecision"],
            llm=self.llm,
        )
        metrics["context_recall"] = instantiate_metric(
            collections,
            ["ContextRecall"],
            llm=self.llm,
        )
        metrics["context_relevance"] = instantiate_metric(
            collections,
            ["ContextRelevance"],
            llm=self.llm,
        )
        metrics["faithfulness"] = instantiate_metric(
            collections,
            ["Faithfulness"],
            llm=self.llm,
        )

        if isinstance(self.embeddings, MetricSetupError):
            metrics["answer_relevancy"] = self.embeddings
        else:
            metrics["answer_relevancy"] = instantiate_metric(
                collections,
                ["AnswerRelevancy", "ResponseRelevancy"],
                llm=self.llm,
                embeddings=self.embeddings,
            )

        if isinstance(self.embeddings, MetricSetupError):
            metrics["answer_correctness"] = self.embeddings
        else:
            answer_correctness_type = (
                getattr(collections, "AnswerCorrectness", None)
                or getattr(collections, "FactualCorrectness", None)
                or getattr(collections, "AnswerAccuracy", None)
            )
            answer_similarity_type = getattr(collections, "AnswerSimilarity", None)
            if answer_correctness_type is None:
                raise RuntimeError("No supported answer correctness metric is available.")
            if answer_similarity_type is None:
                metrics["answer_correctness"] = answer_correctness_type(llm=self.llm, embeddings=self.embeddings)
            else:
                metrics["answer_correctness"] = answer_correctness_type(
                    llm=self.llm,
                    embeddings=self.embeddings,
                    answer_similarity=answer_similarity_type(embeddings=self.embeddings),
                )

        return metrics


def instantiate_metric(module: object, candidate_names: list[str], **kwargs: object) -> object:
    last_error = None
    for name in candidate_names:
        metric_type = getattr(module, name, None)
        if metric_type is None:
            continue

        try:
            return metric_type(**{key: value for key, value in kwargs.items() if value is not None})
        except TypeError as exc:
            last_error = exc
            try:
                return metric_type(kwargs.get("llm"))
            except TypeError as fallback_exc:
                last_error = fallback_exc

    missing = ", ".join(candidate_names)
    if last_error is not None:
        raise RuntimeError(f"Could not instantiate {missing}: {last_error}") from last_error
    raise RuntimeError(f"None of these RAGAS metrics are available: {missing}")


def build_sample(row: dict) -> object:
    ragas_schema = importlib.import_module("ragas.dataset_schema")
    dataset = row.get("dataset") or {}
    response = row.get("response") or {}
    contexts = response.get("retrievedContexts") or []

    return ragas_schema.SingleTurnSample(
        user_input=dataset.get("Question", ""),
        response=response.get("answer", ""),
        retrieved_contexts=[context.get("text", "") for context in contexts if context.get("text")],
        reference=dataset.get("Answer", ""),
        reference_contexts=[dataset.get("Reference Context", "")] if dataset.get("Reference Context") else [],
    )


def normalize_score(value: object) -> float | int | str | None:
    if value is None:
        return None

    result_value = getattr(value, "value", None)
    if result_value is not None:
        return normalize_score(result_value)

    if isinstance(value, (float, int, str)):
        return value

    try:
        return float(value)  # type: ignore[arg-type]
    except (TypeError, ValueError):
        return str(value)


def main() -> int:
    return asyncio.run(main_async())


if __name__ == "__main__":
    raise SystemExit(main())
