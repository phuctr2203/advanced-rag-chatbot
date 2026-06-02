import asyncio
import json
import os
import urllib.request


class TeiEmbeddings:
    def __init__(self, base_url: str, timeout_seconds: int):
        self._url = f"{base_url.rstrip('/')}/embed"
        self._timeout_seconds = timeout_seconds

    def embed_text(self, text: str) -> list[float]:
        return self.embed_texts([text])[0]

    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        request = urllib.request.Request(
            self._url,
            data=json.dumps({"inputs": texts}).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=self._timeout_seconds) as response:
            return json.load(response)

    async def aembed_text(self, text: str) -> list[float]:
        return await asyncio.to_thread(self.embed_text, text)

    async def aembed_texts(self, texts: list[str]) -> list[list[float]]:
        return await asyncio.to_thread(self.embed_texts, texts)


def create_metrics(profile: dict, tei_base_url: str, timeout_seconds: int) -> dict:
    try:
        from openai import AsyncOpenAI
        from ragas.embeddings.base import BaseRagasEmbedding
        from ragas.llms import llm_factory
        from ragas.metrics.collections import (
            AnswerCorrectness,
            AnswerRelevancy,
            ContextPrecision,
            ContextRecall,
            Faithfulness,
        )
    except ImportError as exc:
        raise RuntimeError(
            "RAGAS scoring dependencies are missing. Run: "
            "python -m pip install -r evaluation/requirements.txt"
        ) from exc

    api_key = os.getenv(profile["apiKeyEnvironmentVariable"], "ollama")
    client = AsyncOpenAI(
        api_key=api_key,
        base_url=f"{profile['baseUrl'].rstrip('/')}/v1",
        timeout=timeout_seconds,
    )
    llm = llm_factory(profile["model"], client=client)

    class RagasTeiEmbeddings(BaseRagasEmbedding):
        def __init__(self):
            super().__init__()
            self._client = TeiEmbeddings(tei_base_url, timeout_seconds)

        def embed_text(self, text: str, **kwargs) -> list[float]:
            return self._client.embed_text(text)

        async def aembed_text(self, text: str, **kwargs) -> list[float]:
            return await self._client.aembed_text(text)

        def embed_texts(self, texts: list[str], **kwargs) -> list[list[float]]:
            return self._client.embed_texts(texts)

        async def aembed_texts(self, texts: list[str], **kwargs) -> list[list[float]]:
            return await self._client.aembed_texts(texts)

    embeddings = RagasTeiEmbeddings()
    return {
        "faithfulness": Faithfulness(llm=llm),
        "answer_relevancy": AnswerRelevancy(llm=llm, embeddings=embeddings),
        "context_precision": ContextPrecision(llm=llm),
        "context_recall": ContextRecall(llm=llm),
        "answer_correctness": AnswerCorrectness(llm=llm, embeddings=embeddings),
    }


async def score_result(metrics: dict, row: dict, raw_result: dict) -> dict:
    contexts = [context["text"] for context in raw_result.get("retrievedContexts", [])]
    common = {
        "user_input": row["question"],
        "response": raw_result.get("answer", ""),
        "reference": row.get("referenceAnswer", ""),
        "retrieved_contexts": contexts,
    }
    arguments = {
        "faithfulness": ["user_input", "response", "retrieved_contexts"],
        "answer_relevancy": ["user_input", "response"],
        "context_precision": ["user_input", "reference", "retrieved_contexts"],
        "context_recall": ["user_input", "reference", "retrieved_contexts"],
        "answer_correctness": ["user_input", "response", "reference"],
    }
    scores = {}
    for name, metric in metrics.items():
        result = await metric.ascore(**{key: common[key] for key in arguments[name]})
        scores[name] = {
            "value": float(result.value),
            "reason": getattr(result, "reason", None),
        }
    return scores
