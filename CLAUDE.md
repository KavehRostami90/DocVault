# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DocVault is a **monolith-first document repository** built with **.NET 10 / C# 14** and a **React 18 / TypeScript** frontend. It provides document ingestion, full-text search with vector embeddings, and background indexing via a clean-layered REST API.

## Commands

### Backend (.NET)

```bash
# Restore & build
dotnet restore DocVault.sln
dotnet build DocVault.sln

# Run API (requires PostgreSQL — see Local Dev below)
dotnet run --project src/DocVault.Api

# Run with Docker (PostgreSQL + API together)
docker compose up

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/DocVault.UnitTests
dotnet test tests/DocVault.IntegrationTests

# Run a single test by name filter
dotnet test --filter "FullyQualifiedName~MyTestMethod"

# EF Core migrations (from repo root)
dotnet ef migrations add <Name> --project src/DocVault.Infrastructure --startup-project src/DocVault.Api
dotnet ef database update --project src/DocVault.Infrastructure --startup-project src/DocVault.Api
```

### Frontend (React / Vite)

```bash
cd ui
npm install
npm run dev      # Dev server — proxies /api to backend
npm run build    # Production build (output: ui/dist)
npm run preview  # Preview production build locally
```

### Local Development Setup

The API requires PostgreSQL 16. The easiest path is Docker Compose:

```bash
docker compose up   # starts API on :8080 and PostgreSQL on :5432
```

For running the API outside Docker, create `src/DocVault.Api/appsettings.Development.json` (git-ignored) with:

```json
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
  }
}
```

API docs once running: Scalar UI at `/scalar/v1`, Swagger at `/swagger`.

## Auth & Authorization

Self-hosted ASP.NET Core Identity + JWT. No external IdP.

### Roles
| Role | Access |
|---|---|
| `Admin` | All documents, admin dashboard (`/admin`) |
| `User` | Own documents only |
| `Guest` | Own documents (ephemeral, 24h, `IsGuest=true` in DB) |

### Token flow
- **Access token**: JWT (15 min), stored in React `sessionStorage`
- **Refresh token**: opaque Guid, httpOnly cookie (`SameSite=None;Secure`), 7d (guests: 24h)
- On 401: `client.ts` silently calls `POST /auth/refresh` then retries once
- On refresh failure: redirect to `/login`

### Auth endpoints (`/api/v1/auth/`)
`POST /register`, `POST /login`, `POST /guest`, `POST /refresh`, `POST /logout`, `GET /me`

### Admin endpoints (`/api/v1/admin/`, Admin role only)
`GET /admin/users`, `GET /admin/documents`

### Configuration (override in production via env vars)
```json
"Auth": {
  "JwtSigningKey": "<32+ char secret>",   // required — empty = auth disabled
  "JwtIssuer": "docvault",
  "JwtAudience": "docvault-ui",
  "AccessTokenExpiryMinutes": 15,
  "RefreshTokenExpiryDays": 7,
  "AdminEmail": "admin@docvault.local",
  "AdminPassword": "<secret>"
}
```
Dev defaults are in `appsettings.Development.json` (gitignored). Production values must come from Azure App Settings or environment variables.

### Key files
- `src/DocVault.Infrastructure/Auth/` — `ApplicationUser`, `RefreshToken`, `JwtTokenService`, `IdentitySeeder`, `AuthSettings`, `AppRoles`
- `src/DocVault.Application/Abstractions/Auth/` — `ICurrentUser`, `ITokenService`
- `src/DocVault.Api/Services/CurrentUserService.cs` — resolves user from JWT claims
- `src/DocVault.Api/Endpoints/AuthEndpoints.cs` — auth routes
- `src/DocVault.Api/Endpoints/AdminEndpoints.cs` — admin routes
- `ui/src/contexts/AuthContext.tsx` — React auth state + silent refresh timer
- `ui/src/api/auth.ts` — raw auth API calls (always `credentials: 'include'`)
- `ui/src/api/client.ts` — all API calls; attaches Bearer token; 401→refresh→retry

### Document ownership
`Document.OwnerId` (nullable Guid). All document handlers accept `ICurrentUser` and apply ownership filtering. Admins bypass all ownership checks. Documents created before auth have `OwnerId = null` (admin-only visible).

### CORS + cookies
When `Cors:AllowedOrigins` is a specific list (not `*`), CORS policy automatically adds `.AllowCredentials()` to allow the httpOnly cookie cross-origin.

## Architecture

Clean Architecture + CQRS with this dependency rule: **outer layers depend inward; inner layers never reference outer ones**.

```
DocVault.Api          → Application, Domain          (HTTP surface, DTOs, validators)
DocVault.Application  → Domain                       (use cases, interfaces, worker)
DocVault.Domain       → nothing                      (aggregates, events, value objects)
DocVault.Infrastructure → Application, Domain        (EF Core, storage, extractors, AI)
DocVault.Shared       → nothing                      (cross-cutting utilities placeholder)
```

### Domain Layer

- **Aggregates**: `Document` (root), `ImportJob` (root), `Tag` (entity)
- **Entities**: `DocumentChunk` — one per text window produced during ingestion; holds `ChunkIndex`, `Text`, `Embedding`, `StartChar`, `EndChar`, and a `DocumentId` FK
- **Value objects**: `DocumentId`, `FileHash`, `DocumentStatus`, `RelevanceScore`
- **Domain events**: `DocumentImported`, `DocumentIndexed`, `SearchExecuted`
- **`ValidationConstants`** — single source of truth for all validation limits (title length, file size, tag count, query length). **Never hard-code these values anywhere else.**
- Domain invariants are enforced in aggregate constructors/setters via `DomainException`.

### Application Layer

Orchestrates use cases with no web or EF dependencies. Key abstractions:

| Interface | Dev Implementation | Production Path |
|---|---|---|
| `IFileStorage` | `LocalFileStorage` (`{id}.bin`) | Azure Blob / S3 / MinIO |
| `IEmbeddingProvider` | `FakeEmbeddingProvider` (FNV-1a hash, 768-dim) | OpenAI / Azure OpenAI |
| `ITextChunker` | `SimpleTextChunker` (400-word windows, 80-word overlap) | swap for token-aware or semantic chunker |
| `IDocumentChunkRepository` | `EfDocumentChunkRepository` | same — EF Core with pgvector |
| `IWorkQueue<T>` | `ChannelWorkQueue<T>` (in-memory) | `PostgresWorkQueue` (SKIP LOCKED) |
| `ITextExtractor` | `PlainTextExtractor` + `MarkdownExtractor` | pluggable registry |
| `IDomainEventDispatcher` | `InProcessDomainEventDispatcher` | RabbitMQ / Event Hub |

CQRS handlers: `ImportDocumentHandler`, `DeleteDocumentHandler`, `UpdateTagsHandler`, `StartImportJobHandler`, `GetDocumentHandler`, `ListDocumentsHandler`, `SearchDocumentsHandler`, `GetImportStatusHandler`, `ListTagsHandler`.

### Document Ingestion Flow

```
POST /documents/import
  → ImportDocumentHandler: hash file, store binary, create Document (Imported), create ImportJob (Pending), enqueue work item
  → IndexingWorker (BackgroundService): dequeue → ImportJob (InProgress) → IngestionPipeline:
      FileReadStage → TextExtractStage → ChunkingStage → EmbeddingStage (per chunk) → IndexStage
    → IDocumentChunkRepository.ReplaceAsync() — persist chunk embeddings (delete-then-insert, idempotent)
    → Document.AttachText() + Document.AttachEmbedding(firstChunk) + Document.MarkIndexed()
    → ImportJob (Completed or Failed)
```

On startup, `IndexingWorker` recovers any `Pending`/`InProgress` jobs from the DB (crash recovery).

**Chunk shape:** `DocumentChunk` records carry `StartChar`/`EndChar` offsets into the original extracted text so matched passages can be surfaced verbatim. The `DocumentChunks` table has an HNSW index on the `Embedding vector(768)` column for efficient cosine similarity queries.

### API Layer

Minimal APIs (no MVC controllers). Endpoints registered as extension methods in `Endpoints/`. Middleware pipeline order:

1. Serilog request logging + context enrichment
2. `CorrelationIdMiddleware` → injects `X-Correlation-Id`
3. `GlobalExceptionHandler` → translates to RFC 7807 `ProblemDetails`
4. Endpoint routing with `ValidationFilter` (FluentValidation — runs before handlers)

All routes prefixed `/api/v1`. Health probes at `/health/live` (always 200) and `/health/ready` (checks DB + storage).

## Key Coding Conventions

- **`Result<T>`** — all handler return types; never throw for expected failures
- **Async everywhere** — all I/O is `async Task` with `CancellationToken` parameters
- **Source-generated logging** — use `[LoggerMessage]` attribute, not `logger.Log(...)`
- **Central NuGet versions** — all package versions pinned in `Directory.Packages.props`; never add `Version=` to `<PackageReference>` in `.csproj` files
- **Dependency inversion** — depend on interfaces in Application; register implementations in Infrastructure (via `Composition/` in Api)
- 2-space indentation, LF line endings (`.editorconfig`)

## Infrastructure & Deployment

- **Containerization**: `Dockerfile` (multistage, port 8080) + `docker-compose.yml`
- **Azure IaC**: `infra/main.bicep` — App Service (Free F1, upgrade for production) + PostgreSQL
- **Azure CLI config**: `azure.yaml` (Azure Developer CLI)
- **Frontend**: Azure Static Web Apps (`ui/staticwebapp.config.json` — SPA fallback + security headers)
- **CI** (`.github/workflows/ci.yml`): runs unit + integration tests on PR/push to `master` with a real PostgreSQL 16 service
- **Deploy** (`.github/workflows/deploy.yml`): triggered after CI passes on `master`; deploys Bicep infra then ZIP-deploys the API; requires OIDC secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) and `DATABASE_CONNECTION_STRING`, `OPENAI_API_KEY`
