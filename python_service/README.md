# Python Services

This folder contains Python services that run beside the .NET API.

## Reranker Service

Install dependencies:

```powershell
py -3.12 -m pip install -r python_service\requirements.txt
```

Start the local multilingual reranker:

```powershell
py -3.12 python_service\reranker_service.py `
  --host 127.0.0.1 `
  --port 8081 `
  --model jinaai/jina-reranker-v2-base-multilingual
```

Health check:

```powershell
Invoke-RestMethod http://127.0.0.1:8081/health
```

Smoke test:

```powershell
py -3.12 evaluation\test_reranker_multilingual.py `
  --base-url http://127.0.0.1:8081 `
  --endpoint /rerank `
  --mode rerank `
  --model jinaai/jina-reranker-v2-base-multilingual `
  --timeout 180
```
