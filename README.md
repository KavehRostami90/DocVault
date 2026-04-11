# DocVault

[![CI](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml/badge.svg)](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml)

DocVault is a self-hosted document repository. You upload files (PDF, TXT, DOCX), and DocVault stores them, extracts their text, generates vector embeddings in the background, and lets you search across the full content. Access is controlled by role-based auth — admins see everything, regular users see only their own documents.

## What It Does

- **Upload & store** — multipart file upload; files are SHA-256 hashed and deduplicated before storage
- **Background indexing** — a `BackgroundService` worker extracts text and generates embeddings asynchronously; you can poll job status while it runs
- **Full-text search** — keyword search across titles and extracted text, ranked by relevance score
- **Role-based access** — `Admin`, `User`, and `Guest` (ephemeral 24h) roles; JWT access tokens + httpOnly refresh token cookie
- **Admin dashboard** — separate UI panel for managing all documents and all users
- **React SPA** — Vite + React 18 + Tailwind CSS frontend served alongside the API

## Technology Stack

| Concern | Technology |
|---|---|
| Backend runtime | .NET 10 / C# 14 |
| Web framework | ASP.NET Core 10 — Minimal APIs |
| ORM / database | EF Core 10 + PostgreSQL 16 |
| Auth | ASP.NET Core Identity + JWT |
| Validation | FluentValidation 12 |
| Logging | Serilog 9 (structured JSON) |
| API docs | OpenAPI + Scalar UI + Swagger UI |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS |
| Testing | xUnit 2 + Moq 4 |
| Containerisation | Docker + Docker Compose |

## Running with Docker

Docker Compose starts all three services — PostgreSQL, the .NET API, and the React UI — with a single command.

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

### 2. Start everything

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| UI | http://localhost:3000 |
| API | http://localhost:8080 |
| Scalar UI | http://localhost:8080/scalar/v1 |
| Swagger UI | http://localhost:8080/swagger |

### 3. Log in

Use the `DOCVAULT_ADMIN_EMAIL` and `DOCVAULT_ADMIN_PASSWORD` values from your `.env`. The admin account is seeded automatically on first startup.

### Stopping and cleaning up

```bash
docker compose down          # stop containers, keep volumes
docker compose down -v       # stop containers and delete all data
```

## Local Development (without Docker)

### Backend

Requires .NET 10 SDK and a PostgreSQL 16 instance.

Copy the settings template:

```bash
cp src/DocVault.Api/appsettings.Development.template.json \
   src/DocVault.Api/appsettings.Development.json
```

Fill in your local values, then:

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
dotnet test                          # all tests
dotnet test tests/DocVault.UnitTests
dotnet test tests/DocVault.IntegrationTests
dotnet test --filter "FullyQualifiedName~MyTest"
```

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
└── Dockerfile                   # API multi-stage build
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
               │    ├─ FileReadStage
               │    ├─ TextExtractStage
               │    ├─ EmbeddingStage
               │    └─ IndexStage
               ├─ Document.AttachText() → Document.MarkIndexed()
               └─ ImportJob → Completed / Failed
```

## Auth & Roles

| Role | What they can access |
|---|---|
| `Admin` | All documents, admin dashboard (`/admin`) |
| `User` | Their own documents only |
| `Guest` | Their own documents only; account expires after 24 hours |

- **Access token**: JWT, 15 min lifetime, stored in `sessionStorage`
- **Refresh token**: opaque GUID, httpOnly cookie, 7-day lifetime
- On 401 the client silently calls `POST /auth/refresh` and retries once

## Pluggable Implementations

| Component | Default (Docker / dev) | Production swap |
|---|---|---|
| Embeddings | `FakeEmbeddingProvider` — FNV-1a hashing, 128-dim | OpenAI / Azure OpenAI |
| File storage | `LocalFileStorage` — `{id}.bin` in `/app/storage` | `AzureBlobFileStorage` |
| Work queue | `ChannelWorkQueue<T>` — in-memory | `PostgresWorkQueue` — SKIP LOCKED |
| Index stage | Virtual no-op | Subclass with PostgreSQL `tsvector` or Azure AI Search |

## Health Checks

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — always 200 if the process is running |
| `GET /health/ready` | Readiness — checks database and file storage; returns 503 if either is down |

## Deployment

CI runs on every PR and push to `master` (unit + integration tests with a real PostgreSQL 16 service).

Production deployment targets Azure App Service + PostgreSQL via Bicep IaC (`infra/main.bicep`). The frontend is deployed to Azure Static Web Apps. See [docs/system-design.md](docs/system-design.md) for the full architecture.

## Docs

- [System Design](docs/system-design.md)
- [API Reference](docs/api.md)
- [Data Model](docs/data-model.md)
- [Production Readiness Plan](docs/production-readiness-plan.md)
