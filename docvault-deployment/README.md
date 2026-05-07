# DocVault — Self-Hosted Deployment

DocVault is a self-hosted document repository with full-text and semantic search, AI-powered question answering, background OCR indexing, and role-based access control.

This folder contains everything you need to run DocVault on your own server using **pre-built Docker images** — no source code or build tools required.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| **Docker** ≥ 26 + **Docker Compose** v2 | [Install Docker Desktop](https://docs.docker.com/desktop/install/) |
| **Ollama** (optional) | For local AI embeddings & Q&A — [ollama.com](https://ollama.com) |

> **Memory:** The default stack needs ≈ 1 GB RAM. Add 4–8 GB if running Ollama inside Docker (`--with-ollama` / `--profile ollama`).

---

## Quick Start

### Windows (PowerShell)

```powershell
# 1. Download this folder (or clone the repo)
# 2. Run the installer — it sets up .env, pulls images, and starts everything
.\install.ps1

# With containerised Ollama (embeddings + QA inside Docker):
.\install.ps1 -WithOllama
```

### Linux / macOS (Bash)

```bash
# Make the script executable once
chmod +x install.sh

# 1. Run the installer
./install.sh

# With containerised Ollama:
./install.sh --with-ollama
```

### Manual (any platform)

```bash
# 1. Copy the environment template
cp .env.example .env

# 2. Edit .env — set your passwords, JWT key, and AI provider
#    (see Configuration Reference below)

# 3. Pull images and start
docker compose pull
docker compose up -d
```

Once running, open **http://localhost:3000** in your browser.  
Log in with the admin credentials you set in `.env`.

---

## AI / Embedding Setup

DocVault needs an embedding model to power semantic search and question answering. Choose one option and configure `.env` accordingly.

### Option A — Ollama on your host (recommended for getting started)

1. Install Ollama: <https://ollama.com>
2. Pull the embedding model and a chat model:
   ```bash
   ollama pull nomic-embed-text   # embeddings (≈ 270 MB)
   ollama pull llama3.1           # Q&A / chat (≈ 4.7 GB, or use llama3.2 for a smaller model)
   ```
3. `.env` defaults already point to `host.docker.internal:11434` — no changes needed.

### Option B — Ollama inside Docker

```bash
# Start with the ollama profile (Linux/macOS):
./install.sh --with-ollama

# Or manually:
docker compose --profile ollama run --rm ollama-init   # pull model once
docker compose --profile ollama up -d
```

Update `.env`:
```env
DOCVAULT_OPENAI_BASE_URL=http://ollama:11434/v1
```

> **GPU passthrough (NVIDIA):** Create a `docker-compose.override.yml` alongside `docker-compose.yml` with:
> ```yaml
> services:
>   ollama:
>     deploy:
>       resources:
>         reservations:
>           devices:
>             - driver: nvidia
>               count: all
>               capabilities: [gpu]
> ```

### Option C — OpenAI API

```env
DOCVAULT_OPENAI_BASE_URL=https://api.openai.com/v1
DOCVAULT_OPENAI_API_KEY=sk-...
DOCVAULT_OPENAI_MODEL=text-embedding-3-small
DOCVAULT_OPENAI_DIMENSIONS=1536
```

---

## Configuration Reference

All settings live in `.env`. Copy `.env.example` to `.env` and fill in the values marked **required**.

| Variable | Default | Required | Description |
|---|---|---|---|
| `DOCVAULT_DB_NAME` | `docvault` | | PostgreSQL database name |
| `DOCVAULT_DB_USER` | `docvault` | | PostgreSQL username |
| `DOCVAULT_DB_PASSWORD` | — | ✅ | PostgreSQL password |
| `DOCVAULT_JWT_KEY` | — | ✅ | JWT signing secret (≥ 32 chars) |
| `DOCVAULT_ADMIN_EMAIL` | `admin@example.com` | ✅ | Initial admin account email |
| `DOCVAULT_ADMIN_PASSWORD` | — | ✅ | Initial admin account password |
| `DOCVAULT_UI_PORT` | `3000` | | Host port for the web UI |
| `DOCVAULT_API_PORT` | `8080` | | Host port for the REST API |
| `DOCVAULT_UPLOAD_MAX_FILE_SIZE_BYTES` | `52428800` | | Max upload size (default 50 MB) |
| `DOCVAULT_OPENAI_BASE_URL` | `http://host.docker.internal:11434/v1` | ✅ | Embedding provider base URL |
| `DOCVAULT_OPENAI_API_KEY` | `ollama` | ✅ | API key (`ollama` for local Ollama) |
| `DOCVAULT_OPENAI_MODEL` | `nomic-embed-text` | ✅ | Embedding model name |
| `DOCVAULT_OPENAI_DIMENSIONS` | `0` | | Embedding dimensions (`0` = model default) |
| `DOCVAULT_QA_MODEL` | `llama3.1` | | Chat model for question answering |
| `DOCVAULT_OLLAMA_MODEL` | `nomic-embed-text` | | Model pulled by `ollama-init` |

---

## Updating

Pull the latest images and restart:

```bash
# Windows
.\install.ps1 -Update

# Linux / macOS
./install.sh --update

# Manual
docker compose pull
docker compose up -d
```

---

## Stopping

```bash
# Windows
.\install.ps1 -Down

# Linux / macOS
./install.sh --down

# Manual
docker compose down
```

Data volumes (`db-data`, `app-storage`) are **preserved** on stop. To also remove all data:

```bash
docker compose down -v
```

---

## Health & Logs

```bash
# View logs for all services
docker compose logs -f

# View logs for a specific service
docker compose logs -f api

# Check container status
docker compose ps

# API health endpoints
curl http://localhost:8080/health/live    # always 200 when the process is up
curl http://localhost:8080/health/ready   # 200 when DB is reachable
```

---

## Source Code

DocVault is open source: <https://github.com/KavehRostami90/DocVault>

Docker images are published to GitHub Container Registry:
- **API** — `ghcr.io/kavehrostami90/docvault:latest`
- **UI** — `ghcr.io/kavehrostami90/docvault-ui:latest`
