# DocVault

[![CI](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml/badge.svg)](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml)

DocVault is a self-hosted document repository. You upload files (PDF, DOCX, TXT, Markdown, or images), and DocVault stores them, extracts their text (including OCR for images), generates vector embeddings in the background, and lets you search across the full content — with **semantic (vector) search** when Ollama is available, or full-text keyword search as a fallback. Access is controlled by role-based auth — admins see everything, regular users see only their own documents.

## What It Does

- **Upload & store** — multipart file upload; files are SHA-256 hashed and deduplicated before storage
- **Text extraction** — PDF (PdfPig), DOCX (OpenXml), Markdown, plain text, and image OCR (Tesseract)
- **Background indexing** — a `BackgroundService` worker extracts text and generates embeddings asynchronously; you can poll job status while it runs
- **Semantic search** — vector similarity search via pgvector (`<=>` cosine distance) when Ollama is running; automatically falls back to PostgreSQL full-text search (`tsvector`) or in-memory keyword search in other environments
- **Role-based access** — `Admin`, `User`, and `Guest` (ephemeral 24h) roles; JWT access tokens + httpOnly refresh token cookie
- **Admin dashboard** — separate UI panel for managing all documents and all users, with re-indexing and stats
- **React SPA** — Vite + React 18 + Tailwind CSS frontend served alongside the API

## Technology Stack

| Concern | Technology |
|---|---|
| Backend runtime | .NET 10 / C# 14 |
| Web framework | ASP.NET Core 10 — Minimal APIs |
| ORM / database | EF Core 10 + PostgreSQL 16 with pgvector |
| Vector search | pgvector 0.3 (cosine similarity, HNSW index) |
| Embeddings | Ollama (`nomic-embed-text`, 768-dim) via OpenAI-compatible API |
| OCR | Tesseract 5 (via `Tesseract` NuGet) |
| Auth | ASP.NET Core Identity + JWT |
| Validation | FluentValidation 12 |
| Logging | Serilog 9 (structured JSON, optional Seq sink) |
| API docs | OpenAPI + Scalar UI + Swagger UI |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS |
| Testing | xUnit 2 + Moq 4 |
| Containerisation | Docker + Docker Compose |

## Running with Docker

Docker Compose starts all three services — PostgreSQL (with pgvector), the .NET API, and the React UI — with a single command.

### 1. Create the secrets file

Copy the template and fill in your values:

```bash
cp .env.example .env
```

Or create `.env` at the project root manually:

```env
# Database
DOCVAULT_DB_NAME=docvault
DOCVAULT_DB_USER=docvault
DOCVAULT_DB_PASSWORD=your-db-password

# API auth
DOCVAULT_JWT_KEY=your-jwt-signing-key-min-32-characters!!
DOCVAULT_ADMIN_EMAIL=admin@example.com
DOCVAULT_ADMIN_PASSWORD=your-admin-password
```

`.env` is gitignored — never commit it.

### 2. Install and start Ollama (for semantic search)

Ollama runs on your host machine and is reached from the API container via `host.docker.internal`. If Ollama is not running, the API falls back to full-text search automatically — no configuration change needed.

```bash
# Install from https://ollama.com, then:
ollama pull nomic-embed-text
ollama serve
```

To use a different model or a remote Ollama instance, set `DOCVAULT_OLLAMA_BASE_URL` and/or `DOCVAULT_OLLAMA_MODEL` in your `.env`:

```env
DOCVAULT_OLLAMA_BASE_URL=http://my-ollama-server:11434/v1
DOCVAULT_OLLAMA_MODEL=nomic-embed-text
```

### 3. Start everything

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| UI | http://localhost:3000 |
| API | http://localhost:8081 |
| Scalar UI | http://localhost:8081/scalar/v1 |
| Swagger UI | http://localhost:8081/swagger |

### 4. Log in

Use the `DOCVAULT_ADMIN_EMAIL` and `DOCVAULT_ADMIN_PASSWORD` values from your `.env`. The admin account is seeded automatically on first startup.

### Stopping and cleaning up

```bash
docker compose down          # stop containers, keep volumes
docker compose down -v       # stop containers and delete all data (including DB)
```

> **Note:** If you are upgrading from a version without pgvector, run `docker compose down -v` before starting again so the database is recreated with the vector extension and migration applied.

## Local Development with Azurite (Azure Blob Storage emulator)

By default the API stores uploaded files on local disk (`LocalFileStorage`). You can swap to **Azurite** — a free, open-source emulator for Azure Blob Storage — to develop and test blob storage locally without needing an Azure account.

**Azurite is 100% free.** It is MIT-licensed and maintained by Microsoft.  
GitHub: <https://github.com/Azure/Azurite>

### How it works

`DependencyInjection.cs` checks `ConnectionStrings:AzureBlob` at startup:

- **empty / missing** → `LocalFileStorage` (files on disk, default)
- **set** → `AzureBlobFileStorage` (Azure SDK `BlobContainerClient`)

Azurite is included in `docker-compose.yml` as an **optional profile** — it only starts when you explicitly activate it.

### Enable via Docker Compose (recommended)

1. Copy `.env.example` to `.env` (if you haven't already)
2. Uncomment `DOCVAULT_BLOB_CONN_STR` in `.env`
3. Start the stack with the `azurite` profile:

```bash
docker compose --profile azurite up --build
```

`docker compose up` (without the profile) starts only `api`, `db`, and `ui` and uses local-disk storage. No override file is needed.

| What | Detail |
|---|---|
| `azurite` service | `mcr.microsoft.com/azure-storage/azurite` on ports 10000–10002 |
| `DOCVAULT_BLOB_CONN_STR` | Set in `.env` — picked up by the `api` container automatically |
| `azurite-data` volume | Blob data persists across restarts |

### Enable without Docker (running the API locally)

Install Azurite globally and start it:

```bash
npm install -g azurite
azurite --location ./AzuriteData --loose
```

Then add to `src/DocVault.Api/appsettings.Development.json` (gitignored):

```json
{
  "ConnectionStrings": {
    "AzureBlob": "UseDevelopmentStorage=true"
  },
  "Storage": {
    "ContainerName": "docvault"
  }
}
```

`UseDevelopmentStorage=true` is the Azurite shorthand for `127.0.0.1:10000` with the well-known developer account credentials.

### Azurite data files

The following files/folders produced by Azurite are gitignored:

```
__blobstorage__/
__queuestorage__/
__azurite_db_table__.json
AzuriteConfig/
```

## Local Development (without Docker)

### Prerequisites

- .NET 10 SDK
- PostgreSQL 16 with the pgvector extension (`CREATE EXTENSION vector;`)
- Ollama running locally (optional — search still works without it)
- Tesseract 5 language data files (for OCR; see Tesseract installation notes)

### Backend

Create `src/DocVault.Api/appsettings.Development.json` (gitignored) with your local connection string:

```json
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
  }
}
```

Then:

```bash
dotnet restore
dotnet run --project src/DocVault.Api
```

### Frontend

```bash
cd ui
npm install
npm run dev      # dev server with hot reload at http://localhost:5173
```

`VITE_API_BASE_URL` in `ui/.env.development` controls which API the dev server proxies to.

### Running tests

```bash
dotnet test                              # all tests
dotnet test tests/DocVault.UnitTests
dotnet test tests/DocVault.IntegrationTests
dotnet test --filter "FullyQualifiedName~MyTest"
```

Integration tests use an in-memory database and a `FakeEmbeddingProvider` — no Ollama or PostgreSQL required.

## Project Structure

```
DocVault/
├── src/
│   ├── DocVault.Api/            # Minimal API endpoints, contracts, validators, middleware
│   ├── DocVault.Application/    # Use cases (CQRS), ingestion pipeline, background worker
│   ├── DocVault.Domain/         # Aggregates, value objects, domain events, invariants
│   ├── DocVault.Infrastructure/ # EF Core, file storage, text extractors, embeddings, auth
│   └── DocVault.Shared/         # Cross-cutting utilities placeholder
├── tests/
│   ├── DocVault.UnitTests/      # Domain, handlers, embedding provider
│   └── DocVault.IntegrationTests/ # API endpoint + search integration tests
├── ui/                          # React 18 + TypeScript + Vite frontend
│   ├── src/
│   │   ├── api/                 # Typed API clients (auth, documents, admin)
│   │   ├── contexts/            # AuthContext — JWT state + silent refresh timer
│   │   └── pages/               # Route-level components and admin dashboard
│   ├── Dockerfile               # Multi-stage build → nginx
│   └── nginx.conf               # SPA fallback + /api proxy to api container
├── docs/                        # Design documents
├── infra/                       # Azure Bicep IaC
├── .env                         # Docker secrets (gitignored — see .env.example)
├── docker-compose.yml
└── Dockerfile                   # API multi-stage build (includes Tesseract)
```

## Architecture Overview

Clean Architecture + CQRS. Outer layers depend inward; inner layers never reference outer ones.

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
│  EF Core · Storage · Extractors · Auth   │
└──────────────────────────────────────────┘
```

## Document Lifecycle

```
Upload → ImportDocumentHandler
           ├─ SHA-256 hash (deduplication)
           ├─ Store binary (IFileStorage)
           ├─ Create Document (Status: Imported)
           ├─ Create ImportJob (Status: Pending)
           └─ Enqueue IndexingWorkItem

Background → IndexingWorker (BackgroundService)
               ├─ On startup: recover Pending/InProgress jobs
               ├─ ImportJob → InProgress
               ├─ IngestionPipeline:
               │    ├─ FileReadStage      (read from IFileStorage)
               │    ├─ TextExtractStage   (PDF / DOCX / Markdown / Image / TXT)
               │    ├─ EmbeddingStage     (float[] via IEmbeddingProvider)
               │    └─ IndexStage
               ├─ Document.AttachText() + Document.AttachEmbedding()
               ├─ Document.MarkIndexed()
               └─ ImportJob → Completed / Failed
```

## Search Strategy

Search uses a chain-of-responsibility pattern — the first strategy that `CanHandle()` the current environment wins:

| Strategy | Activated when | Method |
|---|---|---|
| `PgvectorSearchStrategy` | PostgreSQL + embedding available | Cosine similarity (`<=>`) via pgvector HNSW index |
| `PostgresSearchStrategy` | PostgreSQL, no embedding | `tsvector` full-text search with `ts_rank` |
| `InMemorySearchStrategy` | Non-relational DB (tests) | LINQ `Contains` keyword match |

The embedding call in `SearchDocumentsHandler` is wrapped in a try/catch — if Ollama is unreachable, `queryVector` is set to `null` and the next applicable strategy handles the request.

## Supported File Types

| Format | Extractor | Notes |
|---|---|---|
| PDF | `PdfTextExtractor` (PdfPig) | Text extraction from all PDF types |
| DOCX | `DocxTextExtractor` (DocumentFormat.OpenXml) | Word documents |
| Markdown | `MarkdownTextExtractor` | Strips Markdown syntax |
| TXT | `PlainTextExtractor` | UTF-8 plain text |
| Images (PNG, JPG, etc.) | `ImageTextExtractor` (Tesseract OCR) | Requires Tesseract language data |

## Auth & Roles

| Role | What they can access |
|---|---|
| `Admin` | All documents, admin dashboard (`/admin`) |
| `User` | Their own documents only |
| `Guest` | Their own documents only; account expires after 24 hours |

- **Access token**: JWT, 15 min lifetime, stored in `sessionStorage`
- **Refresh token**: opaque GUID, httpOnly cookie, 7-day lifetime (24h for guests)
- On 401 the client silently calls `POST /auth/refresh` and retries once

## Pluggable Implementations

| Component | Current implementation | Alternative |
|---|---|---|
| Embeddings | `OpenAiEmbeddingProvider` — Ollama `nomic-embed-text` (768-dim) | Any OpenAI-compatible API; set `ApiKey`, `BaseUrl`, `Model`, `Dimensions` |
| Text extraction | `FakeEmbeddingProvider` (128-dim FNV-1a hash) | Used in tests / when Ollama unreachable |
| File storage | `LocalFileStorage` — `{id}.bin` in `/app/storage` | `AzureBlobFileStorage` — set `ConnectionStrings:AzureBlob`; use Azurite locally (see above) |
| Work queue | `ChannelWorkQueue<T>` — in-memory | `PostgresWorkQueue` — SKIP LOCKED (multi-instance) |
| Event dispatch | `InProcessDomainEventDispatcher` | RabbitMQ / Azure Event Hub |

## Health Checks

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — always 200 if the process is running |
| `GET /health/ready` | Readiness — checks database and file storage; returns 503 if either is down |

## Deployment

CI runs on every PR and push to `master` (unit + integration tests with a real PostgreSQL 16 + pgvector service).

Production deployment targets Azure App Service + PostgreSQL via Bicep IaC (`infra/main.bicep`). The frontend is deployed to Azure Static Web Apps. See [docs/system-design.md](docs/system-design.md) for the full architecture.

## Docs

- [System Design](docs/system-design.md)
- [API Reference](docs/api.md)
- [Data Model](docs/data-model.md)
- [Production Readiness Plan](docs/production-readiness-plan.md)
