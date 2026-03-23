# DocVault

[![CI](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml/badge.svg)](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml)

DocVault is a **monolith-first document repository** built with **.NET 10 / C# 14**. It provides document ingestion, full-text search, and background indexing through a clean-layered REST API following Clean Architecture and DDD principles.

## Technology Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 / C# 14 |
| Web framework | ASP.NET Core 10 – Minimal APIs |
| ORM / database | EF Core 10 + PostgreSQL 16 |
| Validation | FluentValidation 12 |
| Logging | Serilog 9 (structured, JSON) |
| API docs | OpenAPI + Scalar UI + Swagger UI |
| Testing | xUnit 2 + Moq 4 |
| Containerisation | Docker + Docker Compose |

## Getting Started

```bash
# Prerequisites: .NET 10 SDK, Docker Desktop

# Restore & build
dotnet restore
dotnet build

# Run with Docker (PostgreSQL + API)
docker compose up

# Run tests
dotnet test
```

**Running locally without Docker** requires a PostgreSQL 16 instance and a connection string.
Create `src/DocVault.Api/appsettings.Development.Local.json` (gitignored) with:

```json
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
  }
}
```

Then run:

```bash
dotnet run --project src/DocVault.Api
```

API documentation is available once running:
- **Scalar UI**: `http://localhost:8080/scalar/v1`
- **Swagger UI**: `http://localhost:8080/swagger`
- **OpenAPI spec**: `http://localhost:8080/openapi/v1.json`

## Project Structure

```
DocVault/
├── src/
│   ├── DocVault.Api/           # Minimal API endpoints, contracts, validators, middleware
│   ├── DocVault.Application/   # Use cases (CQRS), ingestion pipeline, background worker
│   ├── DocVault.Domain/        # Aggregates, value objects, domain events, invariants
│   ├── DocVault.Infrastructure/ # EF Core, file storage, text extractors, embeddings
│   └── DocVault.Shared/        # Cross-cutting utilities placeholder
├── tests/
│   ├── DocVault.UnitTests/     # Domain aggregates, handlers, embedding provider
│   └── DocVault.IntegrationTests/ # API endpoint + search integration tests
├── docs/                       # Design documents
├── docker-compose.yml
└── Dockerfile
```

## Architecture Overview

```
┌──────────────────────────────────────────┐
│  DocVault.Api        (Presentation)      │
│  Endpoints · Contracts · Validators      │
├──────────────────────────────────────────┤
│  DocVault.Application  (Use Cases)       │
│  Commands · Queries · Pipeline · Worker  │
├──────────────────────────────────────────┤
│  DocVault.Domain    (Business Logic)     │
│  Aggregates · ValueObjects · Events      │
├──────────────────────────────────────────┤
│  DocVault.Infrastructure  (I/O)          │
│  EF Core · Storage · Extractors · AI     │
└──────────────────────────────────────────┘
```

**Dependency rule:** outer layers depend inward; inner layers never reference outer ones.

## Document Lifecycle

```
Upload → ImportDocumentHandler
           ├─ SHA-256 hash
           ├─ Store binary (IFileStorage)
           ├─ Create Document (Status: Pending → Imported)
           ├─ Create ImportJob (Status: Pending)
           └─ Enqueue IndexingWorkItem

Background → IndexingWorker
               ├─ ImportJob → InProgress
               ├─ IngestionPipeline.RunAsync()
               │    ├─ FileReadStage
               │    ├─ TextExtractStage
               │    ├─ EmbeddingStage
               │    └─ IndexStage
               ├─ Document.AttachText(extractedText)
               ├─ Document → Indexed  (+ DocumentIndexed event)
               └─ ImportJob → Completed / Failed
```

## Development vs Production Stubs

| Component | Development | Production path |
|---|---|---|
| Embeddings | `FakeEmbeddingProvider` — FNV-1a feature hashing, 128-dim L2-normalised | OpenAI / Azure OpenAI / local ONNX |
| File storage | `LocalFileStorage` — writes `{id}.bin` to `/app/storage` | Azure Blob / AWS S3 / MinIO |
| Work queue | `ChannelWorkQueue<T>` — in-memory channel | `PostgresWorkQueue` — SKIP LOCKED |
| Index stage | `IndexStage` — virtual no-op base class | Subclass with PostgreSQL `tsvector` / Azure AI Search |

## Health Checks

Two dedicated probes are available (not listed in Swagger — call directly):

| Endpoint | Purpose | Checks performed |
|---|---|---|
| `GET /health/live` | Liveness — is the process alive? | None (always 200 if running) |
| `GET /health/ready` | Readiness — are dependencies up? | Database (`CanConnectAsync`) + Storage (write/delete probe) |

Both return a structured JSON body:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.012",
  "checks": {
    "database": { "status": "Healthy", "duration": "00:00:00.011", "error": null },
    "storage":  { "status": "Healthy", "duration": "00:00:00.001", "error": null }
  }
}
```

`/health/ready` returns `503 Service Unavailable` when any check fails.

See [docs/api.md](docs/api.md#health) for the full response schema.

## Docs

- [System Design](docs/system-design.md)
- [API Reference](docs/api.md)
- [Data Model](docs/data-model.md)
- [Production Readiness Plan](docs/production-readiness-plan.md)
