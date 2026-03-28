# System Design

## Architecture Style

DocVault is **monolith-first**. All features live in a single deployable unit organised into clean layers following **Clean Architecture** and **CQRS** with **Domain-Driven Design** patterns. The architecture is designed to be split into services if scale demands it, but starts simple.

## Layer Responsibilities

### `DocVault.Domain` — Business Logic
Pure .NET with zero framework dependencies.

- **Aggregates**: `Document` (root), `ImportJob` (root), `Tag`
- **Value objects**: `DocumentId`, `FileHash`, `DocumentStatus`, `ImportStatus`, `RelevanceScore`
- **Domain events**: `DocumentImported`, `DocumentIndexed`, `SearchExecuted`
- **Primitives**: `AggregateRoot<TId>`, `Entity<TId>`, `DomainException`, `ConflictException`
- **Constants**: `ValidationConstants` — single source of truth for all limits
- **Error codes**: `DomainErrorCodes` — machine-readable error strings

### `DocVault.Application` — Use Cases
Orchestrates use cases. Depends on Domain; no web or EF dependencies.

**Abstractions (interfaces / ports):**

| Interface | Purpose |
|---|---|
| `IDocumentRepository` | Document CRUD |
| `IImportJobRepository` | Job CRUD + `GetPendingAsync` |
| `ITagRepository` | Tag read/write |
| `IFileStorage` | Binary blob read/write |
| `ITextExtractor` | Plaintext extraction from streams |
| `IEmbeddingProvider` | Vector embedding generation |
| `IWorkQueue<T>` | Background task queue |
| `IDomainEventDispatcher` | In-process domain event routing |
| `IIngestionPipeline` | Runs all ingestion stages, returns extracted text |

**CQRS handlers:**

| Commands | Queries |
|---|---|
| `ImportDocumentHandler` | `GetDocumentHandler` |
| `DeleteDocumentHandler` | `ListDocumentsHandler` |
| `UpdateTagsHandler` | `SearchDocumentsHandler` |
| `StartImportJobHandler` | `GetImportStatusHandler` |
| | `ListTagsHandler` |

**Common utilities:** `Result<T>`, `Page<T>`, `PageRequest`, `FilterBuilder`, `SortBuilder`

### `DocVault.Infrastructure` — I/O Implementations

| Abstraction | Implementation | Notes |
|---|---|---|
| `IFileStorage` | `LocalFileStorage` | Stores `{DocumentId}.bin` in `/app/storage` |
| `IEmbeddingProvider` | `FakeEmbeddingProvider` | FNV-1a feature hashing, 128-dim, L2-normalised |
| `IWorkQueue<T>` | `ChannelWorkQueue<T>` | In-memory (dev); swap to `PostgresWorkQueue` |
| `IWorkQueue<T>` | `PostgresWorkQueue` | SKIP LOCKED for multi-instance deployments |
| `ITextExtractor` | `PlainTextExtractor`, `MarkdownExtractor` | Pluggable extractor registry |
| `IDomainEventDispatcher` | `InProcessDomainEventDispatcher` | Synchronous dispatch |

**Persistence tables:** `Documents`, `Tags`, `DocumentTags`, `ImportJobs`, `IndexingQueueEntries`

### `DocVault.Api` — HTTP Surface

| Route group | Endpoints |
|---|---|
| Documents | `POST /api/v1/documents/import`, `GET /api/v1/documents`, `GET /api/v1/documents/{id}`, `PUT /api/v1/documents/{id}`, `DELETE /api/v1/documents/{id}` |
| Search | `POST /api/v1/search/documents` |
| Tags | `GET /api/v1/tags` |
| Imports | `POST /api/v1/imports`, `GET /api/v1/imports/{jobId}` |
| Health | `GET /health/live`, `GET /health/ready` (unversioned) |

**Middleware pipeline (order matters):**
1. Serilog request logging + context enrichment
2. `CorrelationIdMiddleware` — injects `X-Correlation-Id`
3. `GlobalExceptionHandler` — translates exceptions to RFC 7807 `ProblemDetails`
4. Endpoint routing with `ValidationFilter` (FluentValidation)

## Document Lifecycle

```
User uploads file
       │
       ▼
ImportDocumentHandler
  ├─ Hash file (SHA-256)
  ├─ Store binary → IFileStorage
  ├─ Create Document aggregate (Status: Pending)
  ├─ Document.MarkImported()        → Status: Imported + DocumentImported event
  ├─ Create ImportJob (Status: Pending, DocumentId set)
  └─ Enqueue IndexingWorkItem(JobId, StoragePath, ContentType)
           │
           ▼
IndexingWorker (BackgroundService)
  ├─ On startup: recover Pending/InProgress jobs (crash recovery)
  ├─ Dequeue IndexingWorkItem
  ├─ ImportJob → InProgress
  ├─ Run IngestionPipeline → returns extracted text string
  │    ├─ FileReadStage      reads stream from IFileStorage
  │    ├─ TextExtractStage   extracts text (PlainText / Markdown)
  │    ├─ EmbeddingStage     generates float[] via IEmbeddingProvider
  │    └─ IndexStage         virtual no-op base (subclass for real search index)
  ├─ ImportJob → Completed
  ├─ Document.AttachText(extractedText)
  ├─ Document.MarkIndexed()         → Status: Indexed + DocumentIndexed event
  └─ On failure: ImportJob → Failed, Document → Failed (stores IndexingError)
```

## Domain Invariants

All business rules are enforced in aggregate constructors and setters, throwing `DomainException` on violation. The API layer validates requests via FluentValidation *before* the handlers run. Both layers reference `ValidationConstants` so limits are never duplicated.

## Key Coding Conventions

- **Minimal APIs** — no MVC controllers; endpoints are extension methods
- **`[LoggerMessage]`** — source-generated structured logging throughout
- **`Result<T>`** — handler return type; never throw for expected failures
- **`async`/`CancellationToken`** — all I/O is async with cancellation support
- **Central NuGet versions** — all package versions pinned in `Directory.Packages.props`
