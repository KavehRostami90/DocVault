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
| `IDocumentRepository` | Document CRUD + search |
| `IImportJobRepository` | Job CRUD + `GetPendingAsync` |
| `ITagRepository` | Tag read/write |
| `IFileStorage` | Binary blob read/write |
| `ITextExtractor` | Plaintext extraction from streams |
| `IEmbeddingProvider` | Vector embedding generation (`float[]`) |
| `IWorkQueue<T>` | Background task queue |
| `IDomainEventDispatcher` | In-process domain event routing |
| `IIngestionPipeline` | Runs all ingestion stages, returns `IngestionResult` |

`IIngestionPipeline.RunAsync` returns `IngestionResult` — a record carrying both the extracted `Text` string and the `Embedding` float array.

**CQRS handlers:**

| Commands | Queries |
|---|---|
| `ImportDocumentHandler` | `GetDocumentHandler` |
| `DeleteDocumentHandler` | `ListDocumentsHandler` |
| `UpdateTagsHandler` | `SearchDocumentsHandler` |
| `StartImportJobHandler` | `GetImportStatusHandler` |
| `ReindexDocumentHandler` | `ListTagsHandler` |
| | `ListUsersHandler` |
| | `GetAdminStatsHandler` |

**Common utilities:** `Result<T>`, `Page<T>`, `PageRequest`, `FilterBuilder`, `SortBuilder`

### `DocVault.Infrastructure` — I/O Implementations

| Abstraction | Implementation | Notes |
|---|---|---|
| `IFileStorage` | `LocalFileStorage` | Stores `{DocumentId}.bin` in `/app/storage` |
| `IEmbeddingProvider` | `OpenAiEmbeddingProvider` | Ollama (`nomic-embed-text`, 768-dim) via OpenAI-compatible API |
| `IEmbeddingProvider` | `FakeEmbeddingProvider` | FNV-1a feature hashing, 128-dim; used in tests / when API key absent |
| `IWorkQueue<T>` | `ChannelWorkQueue<T>` | In-memory channel (default) |
| `IWorkQueue<T>` | `PostgresWorkQueue` | SKIP LOCKED for multi-instance deployments |
| `ITextExtractor` | `PlainTextExtractor` | UTF-8 plain text |
| `ITextExtractor` | `MarkdownTextExtractor` | Strips Markdown syntax |
| `ITextExtractor` | `PdfTextExtractor` | PDF text extraction via PdfPig |
| `ITextExtractor` | `DocxTextExtractor` | DOCX extraction via DocumentFormat.OpenXml |
| `ITextExtractor` | `ImageTextExtractor` | OCR via Tesseract 5 |
| `IDomainEventDispatcher` | `InProcessDomainEventDispatcher` | Synchronous dispatch |
| `IDocumentSearchStrategy` | `PgvectorSearchStrategy` | Cosine similarity (`<=>`) with pgvector HNSW index |
| `IDocumentSearchStrategy` | `PostgresSearchStrategy` | `tsvector` full-text search with `ts_rank` |
| `IDocumentSearchStrategy` | `InMemorySearchStrategy` | LINQ keyword match (tests / SQLite) |

**Persistence tables:** `Documents`, `Tags`, `DocumentTags`, `ImportJobs`, `IndexingQueueEntries`

### `DocVault.Api` — HTTP Surface

| Route group | Endpoints |
|---|---|
| Auth | `POST /auth/register`, `POST /auth/login`, `POST /auth/guest`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me` |
| Documents | `POST /documents`, `GET /documents`, `GET /documents/{id}`, `GET /documents/{id}/preview`, `GET /documents/{id}/download`, `GET /documents/{id}/extracted-text`, `PUT /documents/{id}/tags`, `DELETE /documents/{id}` |
| Search | `POST /search/documents` |
| Tags | `GET /tags` |
| Imports | `POST /imports`, `GET /imports/{jobId}` |
| Admin | `GET /admin/documents`, `DELETE /admin/documents/{id}`, `POST /admin/documents/{id}/reindex`, `GET /admin/documents/{id}/preview`, `GET /admin/documents/{id}/download`, `GET /admin/users`, `DELETE /admin/users/{id}`, `PUT /admin/users/{id}/roles`, `GET /admin/stats` |
| Config | `GET /config/upload` |
| Health | `GET /health/live`, `GET /health/ready` |

All business endpoints are prefixed `/api/v1`. Health endpoints are unversioned.

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
  ├─ Hash file (SHA-256) — reject duplicate if already stored
  ├─ Store binary → IFileStorage
  ├─ Create Document aggregate (Status: Pending → Imported + DocumentImported event)
  ├─ Create ImportJob (Status: Pending, DocumentId set)
  └─ Enqueue IndexingWorkItem(JobId, StoragePath, ContentType)
           │
           ▼
IndexingWorker (BackgroundService)
  ├─ On startup: recover Pending/InProgress jobs (crash recovery)
  ├─ Dequeue IndexingWorkItem
  ├─ ImportJob → InProgress
  ├─ Run IngestionPipeline → returns IngestionResult { Text, Embedding }
  │    ├─ FileReadStage      reads stream from IFileStorage
  │    ├─ TextExtractStage   selects extractor by content type (PDF/DOCX/MD/Image/TXT)
  │    ├─ EmbeddingStage     calls IEmbeddingProvider → float[768]
  │    └─ IndexStage         (virtual no-op base; subclass for alternative search index)
  ├─ ImportJob → Completed
  ├─ Document.AttachText(result.Text)
  ├─ Document.AttachEmbedding(result.Embedding)
  ├─ Document.MarkIndexed()  → Status: Indexed + DocumentIndexed event
  └─ On failure: ImportJob → Failed, Document → Failed (stores IndexingError)
```

### Re-indexing

An admin can call `POST /admin/documents/{id}/reindex` to re-queue any document that is not in `Pending` state. `Document.PrepareForReindex()` resets the document back to `Imported` status so the worker picks it up again.

## Search Architecture

Search uses a **chain-of-responsibility** pattern. `EfDocumentRepository.SearchAsync` iterates the strategy list and delegates to the first one where `CanHandle()` returns `true`:

```
SearchDocumentsHandler
  ├─ Try: queryVector = await IEmbeddingProvider.EmbedAsync(query)
  │        (on failure: log warning, queryVector = null → graceful fallback)
  └─ IDocumentRepository.SearchAsync(terms, page, size, ownerId, queryVector)
         │
         ├─ PgvectorSearchStrategy  ← IsRelational() && queryVector != null
         │    Raw SQL: ORDER BY "Embedding" <=> '[…]'::vector  LIMIT/OFFSET
         │    HNSW index (vector_cosine_ops) accelerates ANN lookup
         │
         ├─ PostgresSearchStrategy  ← IsRelational() && queryVector == null
         │    to_tsquery / ts_rank on SearchVector (tsvector column)
         │
         └─ InMemorySearchStrategy  ← fallback (non-relational / tests)
              LINQ Contains across Title + Text
```

## Domain Invariants

All business rules are enforced in aggregate constructors and setters, throwing `DomainException` on violation. The API layer validates requests via FluentValidation *before* the handlers run. Both layers reference `ValidationConstants` so limits are never duplicated.

## Key Coding Conventions

- **Minimal APIs** — no MVC controllers; endpoints are extension methods
- **`[LoggerMessage]`** — source-generated structured logging throughout
- **`Result<T>`** — handler return type; never throw for expected failures
- **`async`/`CancellationToken`** — all I/O is async with cancellation support
- **Central NuGet versions** — all package versions pinned in `Directory.Packages.props`
- **Domain stays clean** — `Document.Embedding` is `float[]?` (pure .NET); the `Pgvector.Vector` EF conversion lives only in `DocVaultDbContext`

## Frontend (`ui/`)

A **React 18 + TypeScript** SPA in the `ui/` folder, built with **Vite** and styled with **Tailwind CSS**.

### Technology Stack

| Concern | Library / Version |
|---|---|
| Framework | React 18.3 |
| Routing | React Router DOM 6.27 |
| Icons | lucide-react 0.460 |
| Build | Vite 5.4 |
| CSS | Tailwind CSS 3.4 |
| Language | TypeScript 5.6 |

### Pages

| Page | Route | Access |
|---|---|---|
| `LoginPage` | `/login` | Public |
| `RegisterPage` | `/register` | Public |
| `DocumentsPage` | `/documents` | Authenticated |
| `DocumentDetailPage` | `/documents/:id` | Authenticated |
| `SearchPage` | `/search` | Authenticated |
| `AdminDashboardPage` | `/admin` | Admin role only |

`AdminDashboardPage` has three tabs: **Documents** (all users' files + reindex trigger), **Users** (role assignment + delete), and **Stats** (aggregate counts).

### API Client (`ui/src/api/`)

`client.ts` is the base fetch wrapper — it attaches `Authorization: Bearer <token>`, includes `credentials: 'include'` (for the refresh cookie), and retries once after a silent token refresh on `401`.

On top of `client.ts`, individual modules cover: `auth.ts`, `documents.ts`, `search.ts`, `tags.ts`, `imports.ts`, `admin.ts`, and `config.ts`.

### Auth Flow

1. Login / register → access token in `sessionStorage`; refresh token in httpOnly cookie (`SameSite=None; Secure`).
2. `AuthContext` schedules a silent refresh 30 s before token expiry.
3. On `401`: `client.ts` calls `POST /auth/refresh`, stores the new token, retries the original request.

### Production Build

```bash
cd ui
npm run build   # outputs to ui/dist/
```

The API's `Dockerfile` copies `ui/dist/` into the image and serves the SPA as static files; `nginx.conf` handles SPA fallback routing. For Azure, the frontend can also be deployed independently to Azure Static Web Apps (`staticwebapp.config.json` is in `ui/public/`).

## Technology Stack Summary

### Backend NuGet Packages (selected)

| Package | Version | Purpose |
|---|---|---|
| Microsoft.EntityFrameworkCore | 10.x | ORM |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.x | PostgreSQL provider |
| Pgvector | 0.3.2 | pgvector .NET client |
| PdfPig | 0.1.9 | PDF text extraction |
| DocumentFormat.OpenXml | 3.2.0 | DOCX extraction |
| Tesseract | 5.2.0 | OCR (image files) |
| Azure.Storage.Blobs | 12.27.0 | Azure Blob Storage client |
| FluentValidation.AspNetCore | 12.x | Request validation |
| Serilog.AspNetCore | 9.x | Structured logging |
| Scalar.AspNetCore | 2.13.0 | Modern API docs UI |
| Swashbuckle.AspNetCore | 8.1.0 | Swagger / OpenAPI |
| Asp.Versioning.Http | 8.1.1 | API versioning |
| xunit | 2.9.3 | Unit + integration testing |
| Moq | 4.20.72 | Mocking framework |

### Infrastructure

| Concern | Choice |
|---|---|
| Database | PostgreSQL 16 |
| Vector extension | pgvector 0.3.2 |
| Docker image (DB) | `pgvector/pgvector:pg16` |
| Docker image (API) | `mcr.microsoft.com/dotnet/aspnet:10.0` + Tesseract OCR installed |
| Local embeddings | Ollama `nomic-embed-text` (768-dim) via OpenAI-compatible API |
| Cloud embeddings | Any OpenAI-compatible endpoint (OpenAI, Azure OpenAI) |
| CI / CD | GitHub Actions (`.github/workflows/`) |
| IaC (Azure) | Bicep (`infra/main.bicep`) + Azure Developer CLI (`azure.yaml`) |

