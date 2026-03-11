# DocVault – Copilot Instructions

> This file gives GitHub Copilot the context it needs to generate consistent, idiomatic code for the DocVault project.

---

## Project Overview

DocVault is a **monolith-first document repository** built with **.NET 10 / C# 14**. It provides document ingestion, full-text search, and background indexing via a clean-layered REST API.

**Technology stack:**

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

---

## Architecture: Clean Architecture + CQRS

```
┌──────────────────────────────────────────────────────┐
│  DocVault.Api          (Presentation)                │
│  Endpoints · Contracts · Validators · Middleware     │
├──────────────────────────────────────────────────────┤
│  DocVault.Application  (Use Cases)                   │
│  Commands · Queries · Pipeline · Background Worker   │
├──────────────────────────────────────────────────────┤
│  DocVault.Domain       (Business Logic)              │
│  Aggregates · Entities · ValueObjects · Events       │
├──────────────────────────────────────────────────────┤
│  DocVault.Infrastructure  (I/O)                      │
│  EF Core · Repositories · Storage · Extractors       │
├──────────────────────────────────────────────────────┤
│  DocVault.Shared       (Cross-cutting)               │
└──────────────────────────────────────────────────────┘
```

**Dependency rule:** outer layers depend inward; inner layers never reference outer ones.

---

## Layer Responsibilities

### `DocVault.Domain`

Pure business logic — **no framework dependencies**.

- **Aggregates / Entities**: `Document` (aggregate root), `Tag`, `ImportJob` (aggregate root)
- **Value objects**: `DocumentId`, `FileHash`, `DocumentStatus`, `RelevanceScore`
- **Domain events**: `DocumentImported`, `DocumentIndexed`, `SearchExecuted`
- **Primitives**: `AggregateRoot<TId>`, `Entity<TId>`, `DomainException`
- **Validation constants**: `ValidationConstants` – single source of truth for all limits (title length, file size, tag count, query length, etc.)

```csharp
// Domain aggregate example
public class Document : AggregateRoot<DocumentId>
{
    public string Title { get; private set; }       // 1–256 chars
    public string FileName { get; private set; }    // no path traversal
    public string ContentType { get; private set; } // PDF / TXT / DOCX
    public long Size { get; private set; }           // 1 B – 50 MB
    public FileHash Hash { get; private set; }       // SHA-256
    public string Text { get; private set; }         // extracted text
    public DocumentStatus Status { get; private set; }
    public IReadOnlyCollection<Tag> Tags { get; }   // ≤ 20 tags
}
```

---

### `DocVault.Application`

Orchestrates use cases. References Domain; no web / EF dependencies.

**Abstractions (interfaces):**

| Interface | Purpose |
|---|---|
| `IDocumentRepository` | Document CRUD |
| `ITagRepository` | Tag read/write |
| `IImportJobRepository` | Job CRUD + `GetPendingAsync` |
| `IFileStorage` | Binary blob read/write |
| `ITextExtractor` | Plaintext extraction from streams |
| `IEmbeddingProvider` | Vector embedding generation |
| `IWorkQueue<T>` | Background task queue |
| `IDomainEventDispatcher` | In-process domain event routing |
| `IIngestionPipeline` | Orchestrates the ingestion stages |

**Use cases (CQRS handlers):**

```
Commands                          Queries
─────────────────────────         ─────────────────────────────────
ImportDocumentHandler             GetDocumentHandler
DeleteDocumentHandler             ListDocumentsHandler
UpdateTagsHandler                 SearchDocumentsHandler
StartImportJobHandler             GetImportStatusHandler
                                  ListTagsHandler
```

**Ingestion pipeline stages (executed by `IndexingWorker`):**

```
FileReadStage → TextExtractStage → EmbeddingStage → IndexStage
```

**Background worker — `IndexingWorker`:**

1. On startup: recovers `Pending`/`InProgress` jobs from the database (crash recovery).
2. Continuously dequeues `IndexingWorkItem` objects.
3. Marks the `ImportJob` as `InProgress`, runs the ingestion pipeline, then sets it to `Completed` or `Failed`.

**Common utilities:**

- `Result<T>` – railway-oriented success/failure wrapper
- `Page<T>` / `PageRequest` – pagination models
- `FilterBuilder` / `SortBuilder` – dynamic LINQ helpers

---

### `DocVault.Infrastructure`

Concrete implementations wired via DI. References Application + Domain.

**Persistence (EF Core + PostgreSQL):**

| Table | Purpose |
|---|---|
| `Documents` | Document metadata + status |
| `Tags` | Tag definitions |
| `DocumentTags` | Many-to-many join |
| `ImportJobs` | Async job lifecycle |
| `IndexingQueueEntries` | Persistent work queue |

Repositories: `EfDocumentRepository`, `EfTagRepository`, `EfImportJobRepository`.

**Storage:**

- `LocalFileStorage` – writes `{DocumentId}.bin` to `/app/storage` (development)
- Swap to Azure Blob / S3 / MinIO in production via `IFileStorage`

**Text extractors:**

- `PlainTextExtractor` – UTF-8 text
- `MarkdownExtractor` – Markdown-aware extraction

**Embeddings:**

- `FakeEmbeddingProvider` – returns random vectors (development / testing)
- Replace with OpenAI / Azure OpenAI / local model via `IEmbeddingProvider`

**Work queues:**

- `ChannelWorkQueue<T>` – in-memory (single instance, development)
- `PostgresWorkQueue` – database-backed (multi-instance, production)

**Events:**

- `InProcessDomainEventDispatcher` – synchronous in-process dispatch
- `DocumentImportedHandler`, `SearchExecutedHandler` – event handlers

---

### `DocVault.Api`

HTTP surface. References Application + Domain only.

**Endpoint groups:**

| Group | Routes |
|---|---|
| Documents | `POST /documents/import`, `GET /documents`, `GET /documents/{id}`, `PUT /documents/{id}`, `DELETE /documents/{id}` |
| Search | `POST /search` |
| Tags | `GET /tags` |
| Imports | `POST /imports`, `GET /imports/{jobId}` |
| Health | Liveness / Readiness probes |

**Middleware pipeline (order matters):**

1. Serilog request logging + context enrichment
2. `CorrelationIdMiddleware` – injects `X-Correlation-Id` for distributed tracing
3. `GlobalExceptionHandler` – translates exceptions to RFC 7807 `ProblemDetails`
4. Endpoint routing with `ValidationFilter` (FluentValidation)

**Validation:**

- Every request has a corresponding FluentValidation validator
- `ValidationFilter` is applied as an endpoint filter before handlers run
- All limits are read from `ValidationConstants` in the Domain layer

**Contracts (DTOs):**

- `DocumentCreateRequest` – multipart/form-data with file upload
- `DocumentUpdateRequest` – tags array
- `ListDocumentsRequest` – pagination + filtering + sorting
- `SearchRequest` – query + pagination
- `PageResponse<T>` – generic paginated envelope

---

### `DocVault.Shared`

Placeholder for future shared cross-cutting utilities (caching helpers, guard clauses, etc.).

---

## Document Lifecycle

```
User uploads file
       │
       ▼
ImportDocumentHandler
  ├─ Hash file (SHA-256)
  ├─ Store binary → IFileStorage
  ├─ Create Document aggregate (Status: Imported)
  ├─ Create ImportJob (Status: Pending)
  └─ Enqueue IndexingWorkItem
       │
       ▼
IndexingWorker (BackgroundService)
  ├─ Dequeue work item
  ├─ ImportJob → InProgress
  ├─ Run IngestionPipeline
  │    ├─ FileReadStage   → reads stream from storage
  │    ├─ TextExtractStage → extracts text
  │    ├─ EmbeddingStage  → generates vector
  │    └─ IndexStage      → persists text + vector
  └─ ImportJob → Completed / Failed
```

---

## Key Coding Conventions

- **Minimal APIs** – no MVC controllers; endpoints are registered as extension methods
- **Source-generated logging** – use `[LoggerMessage]` attribute for structured log methods
- **`Result<T>`** – all handler return types; never throw for expected failures
- **Domain invariants** – enforce in aggregate constructors and setters; throw `DomainException`
- **`ValidationConstants`** – never hard-code validation limits; always reference this class
- **Async everywhere** – all I/O methods are `async Task`; use `CancellationToken` parameters
- **Dependency inversion** – depend on interfaces in Application; register implementations in Infrastructure
- **Central NuGet versions** – all package versions are pinned in `Directory.Packages.props`

---

## Project File Layout

```
DocVault/
├── src/
│   ├── DocVault.Api/
│   │   ├── Contracts/         # Request & Response DTOs
│   │   ├── Endpoints/         # Minimal API route groups
│   │   ├── Middleware/        # CorrelationId, exception handling
│   │   ├── Validators/        # FluentValidation validators
│   │   └── Program.cs
│   ├── DocVault.Application/
│   │   ├── Abstractions/      # Interfaces (ports)
│   │   ├── Common/            # Result<T>, Page<T>, builders
│   │   ├── Ingestion/         # Pipeline stages
│   │   ├── UseCases/          # Command & query handlers
│   │   └── Workers/           # IndexingWorker
│   ├── DocVault.Domain/
│   │   ├── Aggregates/        # Document, ImportJob
│   │   ├── Events/            # Domain events
│   │   ├── Primitives/        # AggregateRoot, Entity, DomainException
│   │   └── ValueObjects/      # DocumentId, FileHash, etc.
│   ├── DocVault.Infrastructure/
│   │   ├── Persistence/       # DbContext, EF configs, migrations, repos
│   │   ├── Storage/           # LocalFileStorage
│   │   ├── TextExtraction/    # PlainText, Markdown extractors
│   │   ├── AI/                # FakeEmbeddingProvider
│   │   └── Messaging/         # InProcessDispatcher, PostgresWorkQueue
│   └── DocVault.Shared/
├── tests/
│   ├── DocVault.UnitTests/
│   └── DocVault.IntegrationTests/
├── docs/
│   ├── system-design.md
│   ├── api.md
│   ├── data-model.md
│   └── production-readiness-plan.md
├── docker-compose.yml
├── Dockerfile
└── DocVault.sln
```

---

## Running the Project

```bash
# Restore & build
dotnet restore
dotnet build

# Run API (uses in-memory DB by default)
dotnet run --project src/DocVault.Api

# Run with Docker (PostgreSQL + API)
docker-compose up

# Run tests
dotnet test
```

API documentation is available at:

- Scalar UI: `http://localhost:8080/scalar/v1`
- Swagger UI: `http://localhost:8080/swagger`
- OpenAPI spec: `http://localhost:8080/openapi/v1.json`
