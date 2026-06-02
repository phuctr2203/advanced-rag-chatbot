# Policy Bot RAG - Day 1

This folder is a phase-1-only copy of the policy bot project.

Day 1 scope is defined by `docs/phases/phase-1-infrastructure.md`:

- Start Qdrant and HuggingFace TEI with Docker.
- Create the `policy_docs` Qdrant collection with 1024-dimensional cosine vectors.
- Run the ASP.NET Core API scaffold in `src/API`.
- Configure providers through `appsettings.json`.
- Verify LLM, vision, embedding, and vector-store round trips.

## Run

```powershell
docker compose up -d
dotnet run --project src/API/PolicyBot.Api.csproj
```

Swagger is available in development at `http://localhost:5000/swagger`.

Useful verification endpoints:

- `GET /health`
- `POST /verify/llm`
- `POST /verify/vision` with an uploaded `image` form file
- `POST /verify/embedding`
- `POST /verify/vector-roundtrip`
