# Contributing to DocVault

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Docker Desktop (for the database)
- Tesseract 5 (optional — only needed for OCR on image uploads)

See the main README for full install instructions.

## Running locally

```bash
# Start PostgreSQL
docker run -d --name docvault-db \
  -e POSTGRES_DB=docvault -e POSTGRES_USER=docvault -e POSTGRES_PASSWORD=docvault \
  -p 5432:5432 pgvector/pgvector:pg16

# Create local settings (gitignored)
cat > src/DocVault.Api/appsettings.Development.json << 'EOF'
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
  },
  "Auth": {
    "JwtSigningKey": "local-dev-secret-min-32-chars-here!",
    "AdminEmail": "admin@example.com",
    "AdminPassword": "Admin123!"
  }
}
EOF

# Backend (runs migrations + seeds admin on first start)
dotnet run --project src/DocVault.Api

# Frontend — separate terminal
cd ui && npm install && npm run dev
```

API docs at http://localhost:5000/scalar/v1  
UI at http://localhost:5173

## Tests

```bash
dotnet test                                              # all tests
dotnet test tests/DocVault.UnitTests                     # unit only
dotnet test tests/DocVault.IntegrationTests              # integration only
dotnet test --filter "FullyQualifiedName~SearchDocuments" # by name
```

Integration tests use an in-memory database and `FakeEmbeddingProvider`. Neither PostgreSQL nor Ollama is required.

## Adding a migration

```bash
dotnet ef migrations add <Name> \
  --project src/DocVault.Infrastructure \
  --startup-project src/DocVault.Api
```

Run `dotnet ef database update ...` with the same project flags to apply locally.

## Project layout at a glance

```
src/
  DocVault.Domain/          — aggregates, value objects, domain events, ValidationConstants
  DocVault.Application/     — CQRS handlers, background worker, interfaces (no EF, no HTTP)
  DocVault.Infrastructure/  — EF Core, file storage, embeddings, search strategies, auth
  DocVault.Api/             — Minimal API endpoints, request/response records, DI wiring
tests/
  DocVault.UnitTests/       — domain invariants, handler logic
  DocVault.IntegrationTests/ — full API round-trips (in-memory DB)
ui/
  src/api/                  — typed API client modules
  src/pages/                — route-level React components
  src/contexts/AuthContext.tsx — JWT state + silent refresh
docs/
  decisions/                — ADRs
```

## Key conventions

**Result\<T\> — no exceptions for expected failures.** Handlers return `Result<T>`; callers check `.IsSuccess` and handle the `.Error` string. Exceptions are for unexpected failures only.

**Async with CancellationToken everywhere.** All I/O methods accept a `CancellationToken`. Pass it through; never discard it.

**ValidationConstants is the single source of truth.** All validation limits (file size, title length, tag count, query length) live in `DocVault.Domain.Common.ValidationConstants`. Reference those constants from both validators and domain aggregates — never repeat a magic number.

**Source-generated logging.** Add structured log methods as `[LoggerMessage]` partial methods, not `_logger.LogInformation(...)` calls. This avoids string allocations on hot paths.

**Domain stays clean.** `Document.Embedding` is `float[]?` — plain .NET. The `pgvector` type mapping is handled in `DocVaultDbContext` via a value converter. The Domain project has no infrastructure dependencies.

**HNSW query pattern.** The `vec_candidates` CTE must be a bare `ORDER BY embedding <=> @vec LIMIT n` with no JOINs or WHERE clauses. Any predicate added before the ORDER BY causes the planner to skip the HNSW index and fall back to a sequential scan. Ownership filtering happens in the next CTE (`vec_best`) after the ANN pass.

## Adding a new endpoint

1. Add request/response records in `DocVault.Api/Contracts/`.
2. Add a FluentValidation validator in `DocVault.Api/Validation/`. Reference `ValidationConstants` for limits.
3. Add the handler in `DocVault.Application/UseCases/<feature>/`.
4. Register the route in the relevant `*Endpoints.cs` file.
5. Wire the handler in `DocVault.Api/Composition/DependencyInjection.cs` if it's not auto-registered.

## Adding a new file format

1. Implement `ITextExtractor` in `DocVault.Infrastructure/Text/`.
2. Register it in the `CompositeTextExtractor` mapping (keyed by MIME type).
3. Add the MIME type to `ValidationConstants.Documents.AllowedContentTypes`.

## Pluggable implementations

The default DI registration picks implementations based on configuration:

| What to swap | Setting to configure | Implementation |
|---|---|---|
| Embeddings | `Ollama:BaseUrl` | `OpenAiEmbeddingProvider` (otherwise `FakeEmbeddingProvider`) |
| QA / LLM | `OpenAi:ApiKey` | `OpenAiQuestionAnsweringService` (otherwise `FallbackQuestionAnsweringService`) |
| File storage | `ConnectionStrings:AzureBlob` | `AzureBlobFileStorage` (otherwise `LocalFileStorage`) |
| Work queue | PostgreSQL connection present | `PostgresWorkQueue` (otherwise `ChannelWorkQueue`) |
