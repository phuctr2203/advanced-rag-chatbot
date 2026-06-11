#!/usr/bin/env python3
"""Local reranker HTTP service for RAG retrieval experiments."""

from __future__ import annotations

import argparse
import os
from dataclasses import dataclass
from typing import Any

import torch
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from transformers import AutoModelForSequenceClassification, AutoTokenizer


DEFAULT_MODEL = "jinaai/jina-reranker-v2-base-multilingual"


class RerankRequest(BaseModel):
    query: str = Field(min_length=1)
    documents: list[str] = Field(min_length=1)
    model: str | None = None
    top_n: int | None = None
    return_documents: bool = False


class RerankResult(BaseModel):
    index: int
    relevance_score: float
    score: float
    document: str | None = None


class RerankResponse(BaseModel):
    model: str
    results: list[RerankResult]


@dataclass
class RuntimeConfig:
    model_name: str
    device: str
    max_length: int
    batch_size: int
    trust_remote_code: bool


class RerankerRuntime:
    def __init__(self, config: RuntimeConfig) -> None:
        self.config = config
        self.tokenizer: Any | None = None
        self.model: Any | None = None

    def load(self) -> None:
        if self.model is not None and self.tokenizer is not None:
            return

        patch_transformers_compatibility()

        kwargs: dict[str, Any] = {
            "trust_remote_code": self.config.trust_remote_code,
            "dtype": "auto",
        }
        if self.config.model_name.startswith("jinaai/"):
            kwargs["use_flash_attn"] = False

        self.tokenizer = AutoTokenizer.from_pretrained(
            self.config.model_name,
            trust_remote_code=self.config.trust_remote_code,
        )
        self.model = AutoModelForSequenceClassification.from_pretrained(
            self.config.model_name,
            **kwargs,
        )
        self.model.to(self.config.device)
        self.model.eval()

    def rerank(self, query: str, documents: list[str], top_n: int | None, return_documents: bool) -> RerankResponse:
        self.load()
        assert self.model is not None

        pairs = [[query, document] for document in documents]
        scores = self._score_pairs(pairs)
        results = sorted(
            [
                RerankResult(
                    index=index,
                    relevance_score=score,
                    score=score,
                    document=documents[index] if return_documents else None,
                )
                for index, score in enumerate(scores)
            ],
            key=lambda result: result.relevance_score,
            reverse=True,
        )

        if top_n is not None:
            results = results[: max(top_n, 0)]

        return RerankResponse(model=self.config.model_name, results=results)

    def _score_pairs(self, pairs: list[list[str]]) -> list[float]:
        assert self.model is not None
        assert self.tokenizer is not None

        if hasattr(self.model, "compute_score"):
            with torch.no_grad():
                scores = self.model.compute_score(
                    pairs,
                    max_length=self.config.max_length,
                )
            if hasattr(scores, "tolist"):
                scores = scores.tolist()
            return [float(score) for score in scores]

        scores: list[float] = []
        with torch.no_grad():
            for start in range(0, len(pairs), self.config.batch_size):
                batch = pairs[start : start + self.config.batch_size]
                inputs = self.tokenizer(
                    batch,
                    padding=True,
                    truncation=True,
                    max_length=self.config.max_length,
                    return_tensors="pt",
                )
                inputs = {key: value.to(self.config.device) for key, value in inputs.items()}
                logits = self.model(**inputs, return_dict=True).logits.view(-1).float()
                normalized = torch.sigmoid(logits).detach().cpu().tolist()
                scores.extend(float(score) for score in normalized)

        return scores


def create_app(runtime: RerankerRuntime) -> FastAPI:
    app = FastAPI(title="Policy Bot Reranker Service")

    @app.get("/health")
    def health() -> dict[str, Any]:
        return {
            "status": "ok",
            "model": runtime.config.model_name,
            "device": runtime.config.device,
            "loaded": runtime.model is not None,
        }

    @app.post("/rerank")
    def rerank(request: RerankRequest) -> RerankResponse:
        if request.model and request.model != runtime.config.model_name:
            raise HTTPException(
                status_code=400,
                detail=f"Only model {runtime.config.model_name!r} is loaded.",
            )

        if any(not document.strip() for document in request.documents):
            raise HTTPException(status_code=400, detail="Documents must not be empty.")

        try:
            return runtime.rerank(
                request.query,
                request.documents,
                request.top_n,
                request.return_documents,
            )
        except RuntimeError as exc:
            raise HTTPException(status_code=500, detail=str(exc)) from exc

    return app


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Serve a local multilingual reranker over HTTP.")
    parser.add_argument("--model", default=os.environ.get("RERANKER_MODEL", DEFAULT_MODEL))
    parser.add_argument("--host", default=os.environ.get("RERANKER_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("RERANKER_PORT", "8081")))
    parser.add_argument("--max-length", type=int, default=int(os.environ.get("RERANKER_MAX_LENGTH", "1024")))
    parser.add_argument("--batch-size", type=int, default=int(os.environ.get("RERANKER_BATCH_SIZE", "8")))
    parser.add_argument("--device", default=os.environ.get("RERANKER_DEVICE", default_device()))
    parser.add_argument("--no-trust-remote-code", action="store_true")
    parser.add_argument("--preload", action="store_true", help="Load model before starting the server.")
    return parser.parse_args()


def default_device() -> str:
    return "cuda" if torch.cuda.is_available() else "cpu"


def patch_transformers_compatibility() -> None:
    """Patch compatibility gaps between current Transformers and remote model code."""
    try:
        import transformers.models.xlm_roberta.modeling_xlm_roberta as xlm_roberta
    except Exception:
        return

    if not hasattr(xlm_roberta, "create_position_ids_from_input_ids"):
        def create_position_ids_from_input_ids(input_ids, padding_idx, past_key_values_length=0):
            mask = input_ids.ne(padding_idx).int()
            incremental_indices = (torch.cumsum(mask, dim=1).type_as(mask) + past_key_values_length) * mask
            return incremental_indices.long() + padding_idx

        xlm_roberta.create_position_ids_from_input_ids = create_position_ids_from_input_ids


def main() -> int:
    args = parse_args()
    config = RuntimeConfig(
        model_name=args.model,
        device=args.device,
        max_length=args.max_length,
        batch_size=args.batch_size,
        trust_remote_code=not args.no_trust_remote_code,
    )
    runtime = RerankerRuntime(config)
    if args.preload:
        runtime.load()

    app = create_app(runtime)

    import uvicorn

    uvicorn.run(app, host=args.host, port=args.port)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
