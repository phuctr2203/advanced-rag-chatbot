import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import run_ragas_evaluation as runner


class Handler(BaseHTTPRequestHandler):
    evaluation_calls = 0

    def do_POST(self):
        if self.path.endswith("/points/scroll"):
            self.reply({"result": {"points": [{"payload": {"source_file": "test.pdf"}}], "next_page_offset": None}})
            return
        if self.path == "/api/evaluation/query":
            Handler.evaluation_calls += 1
            self.reply(
                {
                    "answer": "The answer.\n\nSOURCES: test.pdf (page 1)",
                    "intent": "POLICY_QUERY",
                    "retrievalRan": True,
                    "noAnswer": False,
                    "retrievedContexts": [{"text": "The answer.", "imagePath": ""}],
                    "language": "en",
                    "sources": [{"file": "test.pdf", "page": 1}],
                    "formDownloads": [],
                }
            )
            return
        self.send_error(404)

    def reply(self, body):
        payload = json.dumps(body).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, *_):
        pass


async def main():
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    root = runner.ROOT / ".claude-build" / "runner-smoke"
    root.mkdir(parents=True, exist_ok=True)
    for filename in ["inventory.json", "raw-results.json"]:
        path = root / filename
        if path.exists():
            path.unlink()
    try:
        config = {
            "policyBotBaseUrl": f"http://127.0.0.1:{server.server_port}",
            "qdrantBaseUrl": f"http://127.0.0.1:{server.server_port}",
            "qdrantCollection": "test",
            "requestTimeoutSeconds": 2,
            "retryCount": 0,
            "concurrency": 1,
        }
        inventory = root / "inventory.json"
        inventory.write_text('[{"sourceFile":"test.pdf"}]', encoding="utf-8")
        preflight = runner.run_preflight(config, inventory)
        assert preflight["indexedFileCount"] == 1
        rows = [
            {
                "id": "eval-test",
                "question": "Question?",
                "language": "en",
                "expectedIntent": "POLICY_QUERY",
                "expectedSources": [{"file": "test.pdf", "page": 1}],
                "expectedFormDownload": None,
                "expectedImage": False,
            }
        ]
        output = root / "raw-results.json"
        await runner.collect_results(config, rows, output)
        await runner.collect_results(config, rows, output)
        assert Handler.evaluation_calls == 1
        result = runner.read_json(output)[0]
        assert result["status"] == "completed"
        assert all(result["checks"].values())
    finally:
        server.shutdown()
    print("Runner smoke test passed.")


if __name__ == "__main__":
    import asyncio

    asyncio.run(main())
