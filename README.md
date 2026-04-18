# DocVault

[![CI](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml/badge.svg)](https://github.com/KavehRostami90/DocVault/actions/workflows/ci.yml)

DocVault is a **self-hosted document repository** with full-text and semantic search, AI-powered question answering, background OCR indexing, and role-based access control. Upload PDFs, Word documents, images, or plain text — DocVault extracts the content, generates vector embeddings, and lets you search or ask questions across your entire library.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [UI — Pages & Capabilities](#ui--pages--capabilities)
- [API Reference](#api-reference)
- [Architecture](#architecture)
- [Document Lifecycle](#document-lifecycle)
- [Search Strategy](#search-strategy)
- [Question Answering (RAG)](#question-answering-rag)
- [Realtime Status Updates](#realtime-status-updates)
- [Supported File Types](#supported-file-types)
- [Auth & Roles](#auth--roles)
- [Pluggable Implementations](#pluggable-implementations)
- [Configuration Reference](#configuration-reference)
- [Health Checks](#health-checks)
- [Project Structure](#project-structure)
- [Deployment](#deployment)

---

## Getting Started

There are two ways to run DocVault: **Docker** (recommended — no manual installs except Docker itself) or **local development** (run each service directly on your machine for a faster edit-reload cycle).

---

### Path A — Docker (recommended)

Everything runs in containers. You only need Docker Desktop installed on your machine.

#### Step 1 — Install Docker Desktop

| OS | Download |
|---|---|
| Windows | https://docs.docker.com/desktop/install/windows-install/ |
| macOS | https://docs.docker.com/desktop/install/mac-install/ |
| Linux | https://docs.docker.com/desktop/install/linux-install/ |

After installation, open Docker Desktop and wait for the engine to start (the whale icon in the system tray turns steady). Verify it works:

```bash
docker --version        # Docker version 26.x or newer
docker compose version  # Docker Compose version v2.x or newer
```

> **Windows users:** Docker Desktop requires WSL 2. The installer enables it automatically. If prompted to restart, do so before continuing.

---

#### Step 2 — Clone the repository

```bash
git clone https://github.com/KavehRostami90/DocVault.git
cd DocVault
```

---

#### Step 3 — Create your secrets file

Copy the example environment file and open it in a text editor:

```bash
cp .env.example .env
```

Edit `.env` and replace the placeholder values:

```env
# Choose any password for the database
DOCVAULT_DB_PASSWORD=my-secure-db-password

# A random string of at least 32 characters — used to sign JWT tokens
# Generate one: openssl rand -base64 32
DOCVAULT_JWT_KEY=replace-this-with-a-long-random-secret-string!!

# The admin account that is seeded automatically on first startup
DOCVAULT_ADMIN_EMAIL=admin@example.com
DOCVAULT_ADMIN_PASSWORD=my-admin-password
```

> The rest of the values in `.env.example` have working defaults for local development and do not need to be changed to get started. `.env` is gitignored — it will never be committed.

---

#### Step 4 — Start the stack

```bash
docker compose up --build
```

Docker will:
1. Pull the PostgreSQL 16 image (with pgvector)
2. Build the .NET API image (installs Tesseract OCR inside the container)
3. Build the React UI image (runs `npm ci && npm run build`, served by nginx)
4. Apply EF Core database migrations automatically on first run
5. Seed the admin account from your `.env` values

First build takes **3–5 minutes** (downloading base images + compiling). Subsequent starts are seconds.

Wait until you see a line like:

```
api  | [INF] Now listening on: http://[::]:8080
```

---

#### Step 5 — Open the app

| What | URL |
|---|---|
| **UI** (main app) | http://localhost:3000 |
| **API** | http://localhost:8081 |
| **Scalar API docs** | http://localhost:8081/scalar/v1 |
| **Swagger UI** | http://localhost:8081/swagger |

Sign in with the `DOCVAULT_ADMIN_EMAIL` and `DOCVAULT_ADMIN_PASSWORD` you set in `.env`.

---

#### Step 6 (optional) — Enable AI features

Without Ollama, DocVault still works fully — search uses PostgreSQL full-text and question answering returns extractive answers from your documents. To unlock **semantic search** and **LLM-powered question answering**, install Ollama on your host machine:

**Install Ollama:** https://ollama.com/download

Then pull the required models and start the server:

```bash
ollama pull nomic-embed-text   # embedding model (~270 MB)
ollama pull llama3.1           # chat model for QA (~4.7 GB, or use llama3.2 ~2 GB)
ollama serve                   # starts on http://localhost:11434
```

Restart the Docker stack — the API detects Ollama automatically via `host.docker.internal:11434`:

```bash
docker compose restart api
```

> **No GPU required.** Ollama runs on CPU. A GPU speeds things up but is entirely optional.

---

#### Stopping and cleaning up

```bash
# Stop containers but keep your database data
docker compose down

# Stop containers AND delete all data (wipes the database)
docker compose down -v
```

---

### Path B — Local Development (no Docker)

Run each service directly on your machine for faster hot-reload iteration. You will need to install each dependency manually.

---

#### Step 1 — Install required tools

**1.1 — .NET 10 SDK**

Download from https://dotnet.microsoft.com/download/dotnet/10.0

Choose the **SDK** (not Runtime) for your OS. After installation, verify:

```bash
dotnet --version   # should print 10.x.x
```

---

**1.2 — Node.js 20 or newer**

Download from https://nodejs.org (choose the LTS version)

Verify:

```bash
node --version   # v20.x.x or newer
npm --version    # 10.x or newer
```

---

**1.3 — PostgreSQL 16 with pgvector**

DocVault requires PostgreSQL 16 and the `pgvector` extension for vector search.

**Option A — Docker (easiest, even in local dev mode):**

Run only the database in a container:

```bash
docker run -d \
  --name docvault-db \
  -e POSTGRES_DB=docvault \
  -e POSTGRES_USER=docvault \
  -e POSTGRES_PASSWORD=docvault \
  -p 5432:5432 \
  pgvector/pgvector:pg16
```

**Option B — Native PostgreSQL:**

- Download PostgreSQL 16 from https://www.postgresql.org/download/
- Install the `pgvector` extension: https://github.com/pgvector/pgvector#installation
- Create a database and user:

```sql
CREATE USER docvault WITH PASSWORD 'docvault';
CREATE DATABASE docvault OWNER docvault;
\c docvault
CREATE EXTENSION vector;
```

Verify the connection:

```bash
psql -h localhost -U docvault -d docvault -c "SELECT extname FROM pg_extension WHERE extname = 'vector';"
# Should return: vector
```

---

**1.4 — Tesseract 5 (for image OCR)**

Tesseract is required to extract text from uploaded image files. If you only upload PDFs or Word documents you can skip this step — the API will still start, it just won't OCR images.

| OS | Install |
|---|---|
| Windows | Download installer from https://github.com/UB-Mannheim/tesseract/wiki — install to `C:\Program Files\Tesseract-OCR`. Tick "English" language data during install. |
| macOS | `brew install tesseract` |
| Ubuntu / Debian | `sudo apt install tesseract-ocr tesseract-ocr-eng` |

Verify:

```bash
tesseract --version   # tesseract 5.x.x
```

---

**1.5 — Ollama (optional, for AI features)**

Without Ollama, search works via PostgreSQL full-text and QA uses extractive answers. To enable semantic search and LLM answers:

Download from https://ollama.com/download, then:

```bash
ollama pull nomic-embed-text   # embedding model
ollama pull llama3.1           # QA model (or llama3.2 for a smaller download)
ollama serve                   # keep this running while you develop
```

---

#### Step 2 — Configure the backend

Create the local settings file (gitignored):

```bash
# From the repo root
cat > src/DocVault.Api/appsettings.Development.json << 'EOF'
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
  },
  "Auth": {
    "JwtSigningKey": "local-dev-secret-key-min-32-characters!!",
    "AdminEmail": "admin@example.com",
    "AdminPassword": "Admin123!"
  }
}
EOF
```

> On Windows, create the file manually in `src\DocVault.Api\appsettings.Development.json` with the same content.

---

#### Step 3 — Run the backend

```bash
# From the repo root
dotnet restore DocVault.sln
dotnet run --project src/DocVault.Api
```

On first run, EF Core applies all migrations and seeds the admin account. You should see:

```
[INF] Applying migrations...
[INF] Admin account seeded: admin@example.com
[INF] Now listening on: http://localhost:5000
```

Verify the API is running:

```bash
curl http://localhost:5000/health/live
# {"status":"Healthy"}
```

API docs: http://localhost:5000/scalar/v1

---

#### Step 4 — Run the frontend

Open a **second terminal**:

```bash
cd ui
npm install          # install dependencies (first time only, ~30 seconds)
npm run dev          # start Vite dev server with hot reload
```

You should see:

```
  VITE v5.x.x  ready in 300ms

  ➜  Local:   http://localhost:5173/
```

Open http://localhost:5173 and sign in with the admin credentials you set in `appsettings.Development.json`.

> The Vite dev server automatically proxies all `/api/*` requests to the .NET backend — you do not need to configure CORS for local development.

---

#### Step 5 — Run the tests

```bash
# From the repo root — runs all test projects
dotnet test DocVault.sln

# Run only unit tests
dotnet test tests/DocVault.UnitTests

# Run only integration tests
dotnet test tests/DocVault.IntegrationTests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~SearchDocumentsHandlerTests"
```

Integration tests use an **in-memory database** and a `FakeEmbeddingProvider` — PostgreSQL and Ollama are **not** required to run the tests.

---

#### Quick reference — all commands

```bash
# Database (Docker shortcut)
docker run -d --name docvault-db -e POSTGRES_DB=docvault -e POSTGRES_USER=docvault \
  -e POSTGRES_PASSWORD=docvault -p 5432:5432 pgvector/pgvector:pg16

# Backend
dotnet restore DocVault.sln
dotnet run --project src/DocVault.Api       # http://localhost:5000

# Frontend (separate terminal)
cd ui && npm install && npm run dev          # http://localhost:5173

# Tests
dotnet test DocVault.sln

# EF Core — add a new migration
dotnet ef migrations add <MigrationName> \
  --project src/DocVault.Infrastructure \
  --startup-project src/DocVault.Api

# EF Core — apply migrations manually
dotnet ef database update \
  --project src/DocVault.Infrastructure \
  --startup-project src/DocVault.Api
```

---

### Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| `docker compose` not found | Old Docker installation uses `docker-compose` (v1) | Upgrade to Docker Desktop 4.x which bundles Compose v2 |
| Port 3000 or 8081 already in use | Another process is using the port | `docker compose down` first, or change the port mapping in `docker-compose.yml` |
| `pgvector` extension not found | PostgreSQL installed without pgvector | Use the `pgvector/pgvector:pg16` Docker image, or follow the pgvector install guide |
| Tesseract not found on Windows | Install path not in `PATH` | Add `C:\Program Files\Tesseract-OCR` to your system `PATH` environment variable |
| Ollama not detected | API can't reach `localhost:11434` | Run `ollama serve` and check http://localhost:11434 is accessible; Docker path uses `host.docker.internal` |
| `dotnet: command not found` | .NET SDK not installed or not in PATH | Download the SDK from https://dotnet.microsoft.com/download and restart your terminal |
| Frontend shows blank page | API URL mismatch | Check `ui/.env.development` — `VITE_API_BASE_URL` should be empty (uses proxy) for local dev |
| Database migrations fail | Schema out of date | Run `dotnet ef database update --project src/DocVault.Infrastructure --startup-project src/DocVault.Api` |

---

## Features

- **Upload & store** — multipart file upload with SHA-256 deduplication; supports PDF, DOCX, Markdown, plain text, and images
- **Background indexing** — an async `BackgroundService` worker extracts text, runs OCR on images, and generates vector embeddings; crash-safe with startup recovery
- **Semantic search** — pgvector cosine similarity search when Ollama is available; automatically falls back to PostgreSQL full-text (`tsvector`) or in-memory keyword search
- **AI question answering** — RAG pipeline over your documents: retrieves relevant chunks, scores them with a hybrid semantic + lexical approach, and sends them to an LLM for a grounded answer with citations
- **Realtime status** — Server-Sent Events (SSE) stream per document so the UI updates live as indexing progresses
- **Role-based access** — `Admin`, `User`, and `Guest` (ephemeral 24 h) roles with JWT + httpOnly cookie refresh
- **User profiles** — display name editing, password change, and admin-level password reset
- **Forgot password flow** — Identity-token-based reset link; email delivery when an SMTP provider is configured, logged to console in dev
- **Admin dashboard** — manage all users (roles, passwords, deletion) and all documents (reindex, bulk delete) with aggregate statistics
- **Clean API** — versioned Minimal API with FluentValidation, OpenAPI docs (Scalar UI + Swagger), and RFC 7807 problem details

---

## Technology Stack

| Concern | Technology |
|---|---|
| Backend runtime | .NET 10 / C# 14 |
| Web framework | ASP.NET Core 10 — Minimal APIs |
| ORM / database | EF Core 10 + PostgreSQL 16 with pgvector |
| Vector search | pgvector 0.3 (cosine distance `<=>`, HNSW index) |
| Embeddings | Ollama `nomic-embed-text` (768-dim) via OpenAI-compatible API |
| OCR | Tesseract 5 (`Tesseract` NuGet + CLI engine) |
| PDF extraction | PdfPig 0.1.9 |
| DOCX extraction | DocumentFormat.OpenXml 3.2 |
| Auth | ASP.NET Core Identity + JWT |
| Validation | FluentValidation 12 |
| Logging | Serilog 9 (structured JSON; optional Seq sink) |
| API docs | OpenAPI + Scalar UI + Swagger UI |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS |
| Realtime | Server-Sent Events (SSE) |
| Testing | xUnit 2 + Moq 4 + coverlet |
| Containerisation | Docker + Docker Compose |
| Cloud / IaC | Azure App Service + Azure Static Web Apps + Bicep |

---

## UI — Pages & Capabilities

The frontend is a React 18 SPA built with TypeScript, Vite, and Tailwind CSS. It communicates with the API using typed clients and handles JWT token management transparently — including silent refresh 60 seconds before expiry.

### Login & Registration

**`/login`** — Email/password sign-in with a "Forgot password?" link next to the password field. Shows a success banner when redirected back after a password reset. Includes a "Try without registering" button that provisions a temporary guest session (24 h lifetime).

**`/register`** — User registration with display name, email, and password. On success the user is immediately authenticated and redirected to their document library.

### Forgot & Reset Password

**`/forgot-password`** — User enters their email address. The API generates an ASP.NET Identity reset token and (in dev) logs the link to the console; in production this triggers an email. The page always shows a success state regardless of whether the email is registered, preventing user enumeration.

**`/reset-password?token=...&email=...`** — Opened from the reset link. Validates that both query parameters are present, then accepts a new password + confirmation. On success navigates back to `/login` with a green confirmation banner.

### Document Library

**`/documents`** — The main document management page:
- **Upload** — click "Import Document" to open an upload modal; drag-and-drop or file picker; title is pre-filled from the filename and can be edited before submitting
- **List view** — paginated table showing title, file type badge, status (`Pending`, `Imported`, `Indexed`, `Failed`), file size, and upload date
- **Filtering** — filter by title (live search), status, and tag
- **Sorting** — click any column header to sort ascending/descending
- **Navigation** — click any row to open the document detail page

**`/documents/:id`** — Full document detail view:
- **Preview** — inline browser-native preview for supported formats (PDF, images, plain text) via a streaming endpoint
- **Download** — download the original file at any time
- **Extracted text** — collapsible panel showing the raw text extracted by the OCR/parsing pipeline
- **Tag management** — add or remove tags inline with a comma-delimited input; saved on blur
- **Realtime indexing status** — while the document is being indexed a live status indicator updates via SSE without polling
- **Delete** — requires confirmation; removes the document and its binary from storage
- **Ask a question** — inline QA panel: type a question about this specific document and receive an AI-generated answer with the source excerpt

### Search

**`/search`** — Unified search and question-answering interface:
- **Search mode** — returns a paginated list of documents ranked by relevance; each result shows title, a 120-character text snippet, and a relevance score (0–100 %). A badge indicates whether the result used semantic vector search or keyword fallback
- **Ask mode** — sends the query through the RAG pipeline; returns a generated answer followed by up to three citation cards (document title, excerpt, relevance score). A badge shows whether the answer is model-generated or an extractive fallback

### Profile

**`/profile`** — Accessible by clicking the user avatar in the sidebar:
- **Account info** — email (read-only), role badge, and "Member since" date
- **Edit display name** — inline edit with keyboard shortcuts (Enter to save, Escape to cancel)
- **Change password** — requires current password; available to all non-guest users
- **Reset password** *(Admin only)* — amber-highlighted card allowing the admin to set a new password for their own account without entering the current one, using the admin bypass endpoint

### Admin Dashboard

**`/admin`** — Three-tab dashboard visible only to users with the `Admin` role:

**Overview tab** — Aggregate statistics cards:
- Total registered users, guest users, admin users
- Total documents broken down by status (Pending, Imported, Indexed, Failed)
- Clickable cards navigate to the filtered Users or Documents tab

**Users tab** — Full user management table:
- Display name, email, join date, account type (Registered / Guest)
- **Role toggles** — click `Admin` or `User` badges to add/remove roles inline
- **Set password** — key icon expands an inline form to set a new password for any registered user (no current password required)
- **Delete user** — with a confirmation dialog

**Documents tab** — All documents across all users:
- Same filtering and sorting as the user-facing library
- **Reindex** — re-queue any document for the indexing pipeline (useful after an extraction failure)
- **Bulk delete / bulk reindex** — checkbox selection with batch actions
- **Preview / download** — admin can preview or download any document regardless of ownership

---

## API Reference

All routes are prefixed `/api/v1`. Requests and responses use JSON unless noted. Errors follow [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) (`application/problem+json`).

Interactive documentation: **Scalar UI** at `/scalar/v1` · **Swagger UI** at `/swagger`

### Authentication

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/auth/register` | Public | Register a new account. Returns access token + user info. |
| `POST` | `/auth/login` | Public | Sign in with email + password. Returns access token + user info. |
| `POST` | `/auth/guest` | Public | Create an anonymous guest session (24 h). Returns access token + user info. |
| `POST` | `/auth/refresh` | Public (cookie) | Silently exchange the httpOnly refresh-token cookie for a new access token. |
| `POST` | `/auth/logout` | Authenticated | Revoke the refresh token and clear the cookie. |
| `GET` | `/auth/me` | Authenticated | Return the current user's profile (id, email, displayName, role, isGuest, createdAt). |
| `PUT` | `/auth/me` | Authenticated | Update the current user's display name. |
| `PUT` | `/auth/me/password` | Authenticated | Change password — requires the current password. Not available to guest accounts. |
| `PUT` | `/auth/me/reset-password` | Admin | Set a new password for the admin's own account without providing the current password. |
| `POST` | `/auth/forgot-password` | Public | Request a password-reset link (rate-limited). Always returns 200 to prevent user enumeration. |
| `POST` | `/auth/reset-password` | Public | Consume a reset token and set a new password (rate-limited). |

### Documents

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/documents` | Authenticated | List own documents. Supports pagination (`page`, `size`), sorting (`sort`, `desc`), and filtering (`title`, `status`, `tag`). |
| `POST` | `/documents` | Authenticated | Upload a document (`multipart/form-data`: `file`, `title`, optional `tags[]`). Returns the new document record and a background import job. |
| `GET` | `/documents/{id}` | Authenticated | Get a single document's full details. |
| `DELETE` | `/documents/{id}` | Authenticated | Delete a document and its stored binary. |
| `PUT` | `/documents/{id}/tags` | Authenticated | Replace the document's tag list. |
| `GET` | `/documents/{id}/preview` | Authenticated | Stream the original file with `Content-Disposition: inline` for browser preview. |
| `GET` | `/documents/{id}/download` | Authenticated | Stream the original file with `Content-Disposition: attachment` for download. |
| `GET` | `/documents/{id}/status-stream` | Authenticated | **SSE stream** — emits status events as the document moves through the indexing pipeline. Completes immediately if the document is already in a terminal state (Indexed / Failed). |
| `GET` | `/documents/{id}/extracted-text` | Authenticated | Return the raw text extracted by the pipeline as `text/plain`. |

### Search

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/search/documents` | Authenticated | Search across documents. Body: `{ query, page, size }`. Response header `X-Search-Mode` indicates `semantic` or `keyword`. Returns ranked results with title, snippet, and relevance score. |

### Question Answering

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/qa/ask` | Authenticated | RAG-based question answering. Body: `{ question, documentId? }`. Returns a generated answer and up to three citations (document title, excerpt, score). Optionally scoped to a single document. |

### Tags

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/tags` | Authenticated | List all tags visible to the current user (own tags for users; all tags for admins). |

### Imports

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/imports` | Authenticated | Start an import job for a previously stored file reference. |
| `GET` | `/imports/{id}` | Authenticated | Get the status of an import job (Pending / InProgress / Completed / Failed). |

### Admin

All admin endpoints require the `Admin` role.

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/stats` | Aggregate statistics: user counts by type, document counts by status. |
| `GET` | `/admin/users` | List all registered users with roles and metadata. |
| `DELETE` | `/admin/users/{id}` | Delete a user and their data. |
| `PUT` | `/admin/users/{id}/roles` | Replace the role list for a user. |
| `POST` | `/admin/users/{id}/reset-password` | Force-set a new password for any user (no current password needed). Audit-logged. |
| `GET` | `/admin/documents` | List all documents across all users. Supports same pagination/filter/sort as `/documents`. |
| `DELETE` | `/admin/documents/{id}` | Delete any document. Audit-logged. |
| `POST` | `/admin/documents/{id}/reindex` | Re-queue any document for the indexing pipeline. Audit-logged. |
| `POST` | `/admin/documents/bulk-delete` | Delete a batch of documents by ID array. Returns succeeded/failed counts. |
| `POST` | `/admin/documents/bulk-reindex` | Re-queue a batch of documents. Returns succeeded/failed counts. |
| `GET` | `/admin/documents/{id}/preview` | Stream any document inline (admin bypass). |
| `GET` | `/admin/documents/{id}/download` | Download any document (admin bypass). |

### Configuration

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/config/upload` | Public | Returns public upload constraints: max file size (bytes), max upload count, allowed MIME types. |

### Health

| Method | Path | Description |
|---|---|---|
| `GET` | `/health/live` | Liveness probe — always 200 if the process is running. Used by Docker and Azure health checks. |
| `GET` | `/health/ready` | Readiness probe — checks database and file storage. Returns 503 if either is unavailable. |

---

## Architecture

Clean Architecture + CQRS. The dependency rule is strictly enforced: outer layers depend inward, inner layers never reference outer ones.

```
┌──────────────────────────────────────────────────┐
│  DocVault.Api          (Presentation)            │
│  Minimal API endpoints · Contracts · Validators  │
│  Middleware · DI composition                     │
├──────────────────────────────────────────────────┤
│  DocVault.Application  (Use Cases)               │
│  Command/Query handlers · Ingestion pipeline     │
│  Background worker · Abstractions (interfaces)   │
├──────────────────────────────────────────────────┤
│  DocVault.Domain       (Business Logic)          │
│  Aggregates · Value objects · Domain events      │
│  Validation constants · Domain exceptions        │
├──────────────────────────────────────────────────┤
│  DocVault.Infrastructure  (I/O)                  │
│  EF Core · File storage · Text extractors        │
│  Embeddings · Auth · Email · Realtime (SSE)      │
└──────────────────────────────────────────────────┘
```

Key design decisions:
- **Result\<T\>** — all handler return types; expected failures never throw
- **Async everywhere** — all I/O is `async Task` with `CancellationToken` propagation
- **Source-generated logging** — `[LoggerMessage]` attributes for zero-allocation structured logs
- **Central NuGet versions** — all package versions pinned in `Directory.Packages.props`
- **`ValidationConstants`** — single source of truth for all validation limits shared across API and domain layers

---

## Document Lifecycle

```
POST /documents
  └─ ImportDocumentHandler
       ├─ SHA-256 hash → deduplicate
       ├─ IFileStorage.StoreAsync()
       ├─ Document created  (Status: Imported)
       ├─ ImportJob created (Status: Pending)
       └─ IWorkQueue.EnqueueAsync()

BackgroundService → IndexingWorker
  ├─ On startup: recover all Pending / InProgress jobs from DB
  └─ Per work item:
       ├─ ImportJob → InProgress
       ├─ IngestionPipeline:
       │    ├─ FileReadStage      — read bytes from IFileStorage
       │    ├─ TextExtractStage   — PDF / DOCX / Markdown / OCR / plain text
       │    ├─ EmbeddingStage     — float[] via IEmbeddingProvider
       │    └─ IndexStage         — persist text + embedding to DB
       ├─ Document.AttachText() + Document.MarkIndexed()
       ├─ ImportJob → Completed
       └─ IDocumentStatusBroadcaster.Publish() → SSE clients notified
```

---

## Search Strategy

Search uses a chain-of-responsibility pattern. The first strategy whose `CanHandle()` returns `true` wins:

| Strategy | Activated when | Method |
|---|---|---|
| `PgvectorSearchStrategy` | PostgreSQL + embedding available | Cosine similarity `<=>` via pgvector HNSW index |
| `PostgresSearchStrategy` | PostgreSQL, no embedding | `tsvector` full-text with `ts_rank` |
| `InMemorySearchStrategy` | In-memory DB (tests) | LINQ `Contains` keyword match |

If Ollama is unreachable when a search query arrives, `IEmbeddingProvider` throws and `SearchDocumentsHandler` catches it — setting `queryVector = null` and falling through to the next strategy automatically.

---

## Question Answering (RAG)

The QA pipeline in `AskQuestionHandler`:

1. **Retrieve** — runs a semantic search for the question across the document library (optionally scoped to a single document by ID)
2. **Chunk** — splits retrieved document text into 700-character windows with 120-character overlap
3. **Score** — hybrid scoring: **70 % semantic similarity** (cosine distance of the chunk embedding vs. the question embedding) + **30 % lexical overlap** (term frequency matching)
4. **Generate** — sends the top-scoring chunks as context to the LLM with a strict system prompt: answer only from the provided context, never hallucinate, cite your sources
5. **Respond** — returns the generated answer plus up to three citation objects (document title, text excerpt, relevance score)

**Implementations:**

| Service | Description |
|---|---|
| `OpenAiQuestionAnsweringService` | Calls any OpenAI-compatible chat/completions endpoint (defaults to local Ollama `llama3.1`) |
| `FallbackQuestionAnsweringService` | Extractive fallback — returns the top-scored chunk directly as the answer when no LLM is configured |

---

## Realtime Status Updates

Document indexing status is streamed to the browser using **Server-Sent Events (SSE)**.

`GET /documents/{id}/status-stream` opens a persistent connection. The `DocumentStatusBroadcaster` (a thread-safe singleton) maintains a set of `Channel<DocumentStatusEvent>` per document ID:

- **Subscribe** — endpoint registers a channel reader when the SSE connection opens
- **Publish** — `IndexingWorker` calls `Publish(documentId, status, error?)` at each pipeline transition
- **Unsubscribe** — the channel is completed when the client disconnects or the document reaches a terminal state (`Indexed` or `Failed`)
- **Fast path** — if the document is already in a terminal state when the SSE endpoint is called, it emits one event immediately and closes the stream

The UI uses this to update the status badge and progress indicator in real time without polling.

---

## Supported File Types

| Format | MIME type(s) | Extractor | Notes |
|---|---|---|---|
| PDF | `application/pdf` | PdfPig | Full text extraction from all PDF types |
| Word | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | DocumentFormat.OpenXml | `.docx` format |
| Markdown | `text/markdown`, `text/x-markdown` | MarkdownExtractor | Strips Markdown syntax before indexing |
| Plain text | `text/plain`, `application/json` | PlainTextExtractor | UTF-8; JSON files treated as text |
| Images | `image/png`, `image/jpeg`, `image/gif`, `image/tiff`, `image/bmp`, `image/webp` | Tesseract OCR | Requires Tesseract 5 + English language data |

Maximum upload size: **50 MB** per file, up to **10 files** per request (configurable via `ValidationConstants`).

---

## Auth & Roles

| Role | Access |
|---|---|
| `Admin` | All documents and users; admin dashboard |
| `User` | Own documents only |
| `Guest` | Own documents only; account + refresh token expire after 24 hours |

**Token flow:**
- **Access token** — JWT (15 min), stored in `sessionStorage`, sent as `Authorization: Bearer`
- **Refresh token** — opaque GUID, stored in an httpOnly `SameSite=None;Secure` cookie, 7-day lifetime (24 h for guests)
- On 401: the API client silently calls `POST /auth/refresh` and retries the original request once; on second failure it redirects to `/login`
- Silent proactive refresh fires 60 seconds before the access token expires

**Password flows:**
- **Change password** — requires the current password (`PUT /auth/me/password`)
- **Admin self-reset** — Admin can bypass the current-password check on their own account (`PUT /auth/me/reset-password`)
- **Admin set user password** — Admin can force-set any user's password from the Users tab (`POST /admin/users/{id}/reset-password`) — audit-logged
- **Forgot password** — any user can request a token-based reset link (`POST /auth/forgot-password` → `POST /auth/reset-password`)

---

## Pluggable Implementations

| Component | Default | Production / Alternative |
|---|---|---|
| **Embeddings** | `FakeEmbeddingProvider` (FNV-1a hash, 128-dim) when Ollama not configured | `OpenAiEmbeddingProvider` — any OpenAI-compatible endpoint; set `Ollama:BaseUrl`, `Ollama:Model`, `Ollama:Dimensions` |
| **QA / LLM** | `FallbackQuestionAnsweringService` (extractive) | `OpenAiQuestionAnsweringService` — set `OpenAi:ApiKey` + `OpenAi:BaseUrl` |
| **File storage** | `LocalFileStorage` — `{id}.bin` on disk | `AzureBlobFileStorage` — set `ConnectionStrings:AzureBlob`; use Azurite locally |
| **Work queue** | `ChannelWorkQueue<T>` — in-memory, single instance | `PostgresWorkQueue` — `SKIP LOCKED` for multi-instance deployments |
| **Event dispatch** | `InProcessDomainEventDispatcher` | RabbitMQ / Azure Event Hub |
| **Email** | `LogEmailService` — logs reset links to the console | Implement `IEmailService` with SendGrid / SMTP and register in DI |

---

## Configuration Reference

All settings can be overridden via environment variables using the standard `__` separator (e.g. `Auth__JwtSigningKey`).

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:Database` | — | PostgreSQL connection string. If absent, uses in-memory DB. |
| `ConnectionStrings:AzureBlob` | — | Azure Blob Storage connection string. If absent, uses local disk storage. |
| `Storage:ContainerName` | `docvault` | Blob container name. |
| `Auth:JwtSigningKey` | — | Required (32+ chars). Empty = auth disabled (dev only). |
| `Auth:JwtIssuer` | `docvault` | JWT `iss` claim. |
| `Auth:JwtAudience` | `docvault-ui` | JWT `aud` claim. |
| `Auth:AccessTokenExpiryMinutes` | `15` | Access token lifetime. |
| `Auth:RefreshTokenExpiryDays` | `7` | Refresh token lifetime (guests: 24 h, hardcoded). |
| `Auth:AdminEmail` | — | Seeded admin account email. |
| `Auth:AdminPassword` | — | Seeded admin account password. |
| `Auth:FrontendBaseUrl` | `http://localhost:5173` | Used to build password-reset links. Override in production. |
| `Ollama:BaseUrl` | `http://localhost:11434/v1` | Ollama (or any OpenAI-compatible) base URL for embeddings. |
| `Ollama:Model` | `nomic-embed-text` | Embedding model name. |
| `Ollama:Dimensions` | `768` | Embedding vector dimensions. |
| `OpenAi:ApiKey` | — | API key for the QA / LLM endpoint. |
| `OpenAi:BaseUrl` | `http://localhost:11434/v1` | LLM endpoint base URL. |
| `Cors:AllowedOrigins` | `*` | Comma-separated allowed origins. Set to specific URL in production to enable cookie CORS. |

---

## Health Checks

| Endpoint | Purpose | Response |
|---|---|---|
| `GET /health/live` | Liveness — is the process running? | Always `200 OK` |
| `GET /health/ready` | Readiness — can the app serve traffic? | `200 OK` (all checks pass) or `503` (any check fails) |

Readiness checks: **database** connectivity and **file storage** reachability. Used by Docker `HEALTHCHECK` and Azure App Service health probes.

---

## Project Structure

```
DocVault/
├── src/
│   ├── DocVault.Api/              # HTTP surface
│   │   ├── Endpoints/             # Minimal API route registrations
│   │   ├── Contracts/             # Request / response records
│   │   ├── Validation/            # FluentValidation validators
│   │   ├── Middleware/            # Correlation ID, global exception handler
│   │   ├── Mappers/               # Domain → response DTO mappers
│   │   └── Composition/           # DI wiring, auth policies, rate limits
│   ├── DocVault.Application/      # Use cases
│   │   ├── UseCases/              # Command/query handlers (CQRS)
│   │   ├── Abstractions/          # Interfaces (IFileStorage, IEmailService, …)
│   │   └── Background/            # IndexingWorker + IngestionPipeline stages
│   ├── DocVault.Domain/           # Pure business logic — no framework deps
│   │   ├── Documents/             # Document aggregate + value objects
│   │   ├── Events/                # Domain events
│   │   └── Common/                # ValidationConstants, Result<T>, DomainException
│   ├── DocVault.Infrastructure/   # I/O implementations
│   │   ├── Auth/                  # Identity, JWT, refresh tokens
│   │   ├── Email/                 # LogEmailService (swap for real SMTP)
│   │   ├── Embeddings/            # OpenAiEmbeddingProvider, FakeEmbeddingProvider
│   │   ├── Persistence/           # EF Core DbContext, repositories, migrations
│   │   ├── Realtime/              # DocumentStatusBroadcaster (SSE)
│   │   ├── Storage/               # LocalFileStorage, AzureBlobFileStorage
│   │   ├── Text/                  # CompositeTextExtractor + format extractors
│   │   └── Qa/                    # OpenAiQuestionAnsweringService
│   └── DocVault.Shared/           # Cross-cutting utilities placeholder
├── tests/
│   ├── DocVault.UnitTests/        # Domain invariants, handlers, embedding
│   └── DocVault.IntegrationTests/ # API endpoint + search pipeline integration
├── ui/                            # React 18 + TypeScript SPA
│   ├── src/
│   │   ├── api/                   # Typed API clients (auth, documents, admin)
│   │   ├── contexts/              # AuthContext — JWT state + silent refresh
│   │   ├── components/            # Layout, ProtectedRoute, ConfirmDialog, …
│   │   └── pages/                 # Route-level page components
│   ├── Dockerfile                 # Multi-stage build → nginx
│   └── nginx.conf                 # SPA fallback + /api reverse proxy
├── infra/                         # Azure Bicep IaC (App Service + PostgreSQL)
├── docs/                          # Design documents
├── .github/workflows/             # CI, deploy-test, deploy-production, SWA cleanup
├── docker-compose.yml
└── Dockerfile                     # API multi-stage build (includes Tesseract)
```

---

## Deployment

### CI / CD

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | PR to `master`, push to `master` | Builds and runs all unit + integration tests with a real PostgreSQL 16 + pgvector service |
| `deploy-test.yml` | CI passes on `master` | Deploys API to Azure App Service (test environment) + UI to Azure Static Web Apps |
| `deploy-production.yml` | Push a `v*.*.*` tag | Deploys API + UI to production; creates a GitHub Release with auto-generated notes |
| `azure-static-web-apps-white-hill-09bd03303.yml` | PR closed | Cleans up the SWA staging preview environment |

### Azure Infrastructure

Provisioned by `infra/main.bicep`:
- **Azure App Service** — Linux, .NET 10 runtime; health check at `/health/live`; all secrets injected via App Settings
- **Azure Static Web Apps** — hosts the React SPA with SPA fallback routing and security headers

Production secrets required in GitHub Actions environment:
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `DATABASE_CONNECTION_STRING`, `AUTH_JWT_SIGNING_KEY`, `AUTH_ADMIN_PASSWORD`, `OPENAI_API_KEY`

### Further reading

- [System Design](docs/system-design.md)
- [API Reference](docs/api.md)
- [Data Model](docs/data-model.md)
- [Production Readiness Plan](docs/production-readiness-plan.md)
