#!/usr/bin/env python3
"""Smoke-test whether a reranker handles multilingual queries consistently."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin
from urllib.request import Request, urlopen


DEFAULT_CONFIG = Path("evaluation/config.example.json")


@dataclass(frozen=True)
class TestCase:
    language: str
    question: str
    expected_index: int


DEFAULT_DOCUMENTS = [
    (
        "Annual leave policy: Employees receive additional annual leave days "
        "based on seniority and years of service."
    ),
    (
        "Payment request procedure: Payment requests must include an invoice, "
        "approval, and supporting documents before submission."
    ),
    (
        "Fire safety guidance: Fire extinguishers, fire alarms, and emergency "
        "exits must remain accessible in the office building."
    ),
    (
        "Remote work policy: Employees working remotely must follow information "
        "security rules and protect company devices."
    ),
]

DEFAULT_TEST_CASES = [
    TestCase(
        "EN",
        "How many additional annual leave days do employees get based on seniority?",
        0,
    ),
    TestCase(
        "VI",
        "Nhân viên được cộng thêm bao nhiêu ngày phép theo thâm niên?",
        0,
    ),
    TestCase(
        "FR",
        "Combien de jours de congé supplémentaires les employés obtiennent-ils selon l'ancienneté ?",
        0,
    ),
    TestCase(
        "DE",
        "Wie viele zusätzliche Urlaubstage erhalten Mitarbeitende je nach Dienstalter?",
        0,
    ),
]


def main() -> int:
    args = parse_args()
    config = load_config(Path(args.config))
    reranker_config = config.get("reranker") or {}

    base_url = args.base_url or reranker_config.get("baseUrl")
    endpoint = args.endpoint or reranker_config.get("endpoint", "/rerank")
    api_key = args.api_key if args.api_key is not None else reranker_config.get("apiKey", "")
    model = args.model or reranker_config.get("model", "bge-reranker")
    timeout = args.timeout or int(reranker_config.get("timeoutSeconds", 60))
    mode = args.mode or reranker_config.get("mode", "rerank")

    if not base_url:
        print("Missing reranker base URL. Set reranker.baseUrl in config or pass --base-url.", file=sys.stderr)
        return 2

    documents = load_documents(Path(args.documents)) if args.documents else DEFAULT_DOCUMENTS
    test_cases = load_test_cases(Path(args.cases)) if args.cases else DEFAULT_TEST_CASES

    url = build_url(base_url, endpoint)
    print(f"Reranker URL: {url}")
    print(f"Model: {model}")
    print(f"Mode: {mode}")
    print(f"Documents: {len(documents)}")
    print()

    failures = 0
    for case in test_cases:
        try:
            if mode == "chat-completions":
                scores = rank_with_chat_completions(url, model, case.question, documents, api_key, timeout)
            else:
                scores = rerank(url, model, case.question, documents, api_key, timeout)
        except Exception as exc:  # noqa: BLE001 - print actionable endpoint errors.
            print(f"{case.language}: ERROR {type(exc).__name__}: {exc}")
            failures += 1
            continue

        ranked = sorted(scores, key=lambda item: item["score"], reverse=True)
        expected_rank = next(
            (index + 1 for index, item in enumerate(ranked) if item["index"] == case.expected_index),
            None,
        )
        top = ranked[0] if ranked else {"index": None, "score": float("nan")}
        status = "PASS" if expected_rank == 1 else "FAIL"
        if status == "FAIL":
            failures += 1

        print(f"{case.language}: {status}")
        print(f"  Question: {case.question}")
        print(f"  Expected document index: {case.expected_index}")
        print(f"  Expected rank: {expected_rank}")
        print(f"  Top index: {top['index']} score={format_score(top['score'])}")
        print("  Ranking:")
        for item in ranked[: min(len(ranked), args.show_top)]:
            preview = documents[item["index"]].replace("\n", " ")[:100]
            print(f"    #{item['rank']:>2} doc={item['index']} score={format_score(item['score'])} {preview}")
        print()

    if failures:
        print(f"Result: {failures}/{len(test_cases)} language cases failed.")
        return 1

    print("Result: all language cases ranked the expected document first.")
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Check whether a hosted reranker ranks the correct chunk for EN/VI/FR/DE queries."
    )
    parser.add_argument("--config", default=str(DEFAULT_CONFIG), help="Evaluation config JSON.")
    parser.add_argument("--base-url", help="Reranker base URL. Overrides config.")
    parser.add_argument("--endpoint", help="Reranker endpoint path. Defaults to config or /rerank.")
    parser.add_argument("--api-key", help="Reranker API key. Overrides config.")
    parser.add_argument("--model", help="Reranker model name. Overrides config.")
    parser.add_argument(
        "--mode",
        choices=["rerank", "chat-completions"],
        help="Use a native rerank endpoint or an OpenAI-compatible chat-completions endpoint.",
    )
    parser.add_argument("--timeout", type=int, help="HTTP timeout seconds.")
    parser.add_argument("--documents", help="Optional JSON file with a list of candidate document strings.")
    parser.add_argument("--cases", help="Optional JSON file with test cases.")
    parser.add_argument("--show-top", type=int, default=4, help="Number of ranked documents to print.")
    return parser.parse_args()


def load_config(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def load_documents(path: Path) -> list[str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list) or not all(isinstance(item, str) for item in data):
        raise ValueError("--documents must be a JSON array of strings.")
    return data


def load_test_cases(path: Path) -> list[TestCase]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError("--cases must be a JSON array.")

    cases = []
    for item in data:
        cases.append(
            TestCase(
                language=str(item["language"]),
                question=str(item["question"]),
                expected_index=int(item["expected_index"]),
            )
        )
    return cases


def build_url(base_url: str, endpoint: str) -> str:
    if endpoint.startswith("http://") or endpoint.startswith("https://"):
        return endpoint
    return urljoin(base_url.rstrip("/") + "/", endpoint.lstrip("/"))


def rerank(
    url: str,
    model: str,
    query: str,
    documents: list[str],
    api_key: str,
    timeout: int,
) -> list[dict[str, Any]]:
    payload = {
        "model": model,
        "query": query,
        "documents": documents,
        "top_n": len(documents),
        "return_documents": False,
    }
    body = json.dumps(payload).encode("utf-8")
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json",
    }
    if api_key:
        headers["Authorization"] = f"Bearer {api_key}"

    request = Request(url, data=body, headers=headers, method="POST")
    try:
        with urlopen(request, timeout=timeout) as response:  # noqa: S310 - user-provided internal URL.
            response_body = response.read().decode("utf-8")
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code}: {detail}") from exc
    except URLError as exc:
        raise RuntimeError(str(exc.reason)) from exc

    data = json.loads(response_body)
    return parse_scores(data, documents)


def rank_with_chat_completions(
    url: str,
    model: str,
    query: str,
    documents: list[str],
    api_key: str,
    timeout: int,
) -> list[dict[str, Any]]:
    prompt = build_chat_ranking_prompt(query, documents)
    payload = {
        "model": model,
        "messages": [
            {
                "role": "system",
                "content": (
                    "You rank candidate contexts for retrieval evaluation. "
                    "Return only compact JSON. Do not explain."
                ),
            },
            {"role": "user", "content": prompt},
        ],
        "temperature": 0,
        "max_tokens": 200,
    }
    body = json.dumps(payload).encode("utf-8")
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json",
    }
    if api_key:
        headers["Authorization"] = f"Bearer {api_key}"

    request = Request(url, data=body, headers=headers, method="POST")
    try:
        with urlopen(request, timeout=timeout) as response:  # noqa: S310 - user-provided internal URL.
            response_body = response.read().decode("utf-8")
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code}: {detail}") from exc
    except URLError as exc:
        raise RuntimeError(str(exc.reason)) from exc

    data = json.loads(response_body)
    content = extract_chat_content(data)
    ranking = parse_chat_ranking(content)
    scores = []
    total = max(len(ranking), 1)
    for rank, index in enumerate(ranking, start=1):
        if 0 <= index < len(documents):
            scores.append(
                {
                    "index": index,
                    "score": float(total - rank + 1) / total,
                    "rank": rank,
                }
            )

    seen = {item["index"] for item in scores}
    for index in range(len(documents)):
        if index not in seen:
            scores.append({"index": index, "score": 0.0, "rank": len(scores) + 1})

    if not scores:
        raise ValueError(f"Could not parse chat ranking from response content: {content}")
    return scores


def build_chat_ranking_prompt(query: str, documents: list[str]) -> str:
    candidates = "\n".join(
        f"{index}. {document}" for index, document in enumerate(documents)
    )
    return (
        "Rank these candidate contexts by relevance to the question.\n"
        "Return only JSON in this exact shape: {\"ranking\":[0,1,2,3]}.\n"
        "The best candidate index must come first.\n\n"
        f"Question: {query}\n\n"
        f"Candidate contexts:\n{candidates}"
    )


def extract_chat_content(data: dict[str, Any]) -> str:
    choices = data.get("choices")
    if not isinstance(choices, list) or not choices:
        raise ValueError(f"Chat response does not contain choices: {data}")
    message = choices[0].get("message") if isinstance(choices[0], dict) else None
    if isinstance(message, dict):
        content = message.get("content")
        if isinstance(content, str):
            return content.strip()
    text = choices[0].get("text") if isinstance(choices[0], dict) else None
    if isinstance(text, str):
        return text.strip()
    raise ValueError(f"Chat response does not contain message content: {data}")


def parse_chat_ranking(content: str) -> list[int]:
    cleaned = content.strip()
    if cleaned.startswith("```"):
        cleaned = cleaned.strip("`")
        if cleaned.lower().startswith("json"):
            cleaned = cleaned[4:].strip()

    try:
        data = json.loads(cleaned)
    except json.JSONDecodeError:
        start = cleaned.find("{")
        end = cleaned.rfind("}")
        if start < 0 or end <= start:
            raise
        data = json.loads(cleaned[start : end + 1])

    ranking = data.get("ranking")
    if not isinstance(ranking, list):
        raise ValueError(f"Ranking JSON missing ranking list: {content}")
    return [int(item) for item in ranking]


def parse_scores(data: Any, documents: list[str]) -> list[dict[str, Any]]:
    raw_results = extract_result_list(data)
    scores = []

    for fallback_index, item in enumerate(raw_results):
        if not isinstance(item, dict):
            continue

        index = extract_index(item, fallback_index, documents)
        score = extract_score(item)
        if index is None or score is None:
            continue

        scores.append(
            {
                "index": index,
                "score": float(score),
                "rank": int(item.get("rank", fallback_index + 1)),
            }
        )

    if not scores:
        raise ValueError(f"Could not parse reranker scores from response: {data}")

    return scores


def extract_result_list(data: Any) -> list[Any]:
    if isinstance(data, list):
        return data

    if not isinstance(data, dict):
        raise ValueError("Reranker response must be an object or array.")

    for key in ("results", "data", "scores", "rankings"):
        value = data.get(key)
        if isinstance(value, list):
            return value

    if "relevance_scores" in data and isinstance(data["relevance_scores"], list):
        return [
            {"index": index, "score": score}
            for index, score in enumerate(data["relevance_scores"])
        ]

    raise ValueError("Response did not contain results, data, scores, rankings, or relevance_scores.")


def extract_index(item: dict[str, Any], fallback_index: int, documents: list[str]) -> int | None:
    for key in ("index", "document_index", "doc_index", "id"):
        if key in item:
            try:
                return int(item[key])
            except (TypeError, ValueError):
                pass

    document = item.get("document")
    if isinstance(document, dict):
        for key in ("index", "id"):
            if key in document:
                try:
                    return int(document[key])
                except (TypeError, ValueError):
                    pass
        text = document.get("text") or document.get("content")
        if isinstance(text, str) and text in documents:
            return documents.index(text)

    if isinstance(document, str) and document in documents:
        return documents.index(document)

    return fallback_index if fallback_index < len(documents) else None


def extract_score(item: dict[str, Any]) -> float | None:
    for key in ("relevance_score", "score", "logit", "similarity"):
        if key in item:
            try:
                return float(item[key])
            except (TypeError, ValueError):
                return None
    return None


def format_score(score: float) -> str:
    return f"{score:.4f}"


if __name__ == "__main__":
    raise SystemExit(main())
