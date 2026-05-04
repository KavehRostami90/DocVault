#!/usr/bin/env bash
# DocVault installer for Linux / macOS
#
# Usage:
#   ./install.sh                 Install and start DocVault
#   ./install.sh --with-ollama   Also start the containerised Ollama service
#   ./install.sh --update        Pull latest images and restart (upgrade in-place)
#   ./install.sh --down          Stop and remove containers (data is preserved)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"
ENV_FILE="$SCRIPT_DIR/.env"

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; YELLOW='\033[1;33m'; GREEN='\033[0;32m'; CYAN='\033[0;36m'; NC='\033[0m'
header() { echo -e "\n${CYAN}  === $1 ===${NC}\n"; }
step()   { echo -e "${GREEN}  >>  $1${NC}"; }
warn()   { echo -e "${YELLOW}  **  $1${NC}"; }
fail()   { echo -e "${RED}  !!  $1${NC}"; }

# ── Parse arguments ───────────────────────────────────────────────────────────
WITH_OLLAMA=false
DO_UPDATE=false
DO_DOWN=false

for arg in "$@"; do
    case "$arg" in
        --with-ollama) WITH_OLLAMA=true ;;
        --update)      DO_UPDATE=true ;;
        --down)        DO_DOWN=true ;;
        *)
            echo "Unknown argument: $arg"
            echo "Usage: $0 [--with-ollama] [--update] [--down]"
            exit 1 ;;
    esac
done

PROFILES=()
$WITH_OLLAMA && PROFILES=(--profile ollama)

# ── Down ──────────────────────────────────────────────────────────────────────
if $DO_DOWN; then
    header 'Stopping DocVault'
    docker compose -f "$COMPOSE_FILE" "${PROFILES[@]}" down
    echo -e '  Services stopped. Data volumes are preserved.'
    exit 0
fi

header 'DocVault Installer'

# ── Check Docker ──────────────────────────────────────────────────────────────
step 'Checking prerequisites...'

if ! command -v docker &>/dev/null; then
    fail 'Docker is not installed.'
    echo '  Install from: https://docs.docker.com/desktop/install/'
    exit 1
fi

if ! docker info &>/dev/null; then
    fail 'Docker daemon is not running. Please start Docker and try again.'
    exit 1
fi

step "Docker OK  ($(docker --version))"

# ── Update path ───────────────────────────────────────────────────────────────
if $DO_UPDATE; then
    step 'Pulling latest images...'
    docker compose -f "$COMPOSE_FILE" "${PROFILES[@]}" pull

    step 'Restarting services...'
    docker compose -f "$COMPOSE_FILE" "${PROFILES[@]}" up -d

    header 'DocVault updated successfully'
    exit 0
fi

# ── Configure .env ────────────────────────────────────────────────────────────
if [[ ! -f "$ENV_FILE" ]]; then
    step 'Creating .env from .env.example...'
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    warn 'IMPORTANT: Edit .env and set your passwords and API keys.'
    echo ''
    echo "  File: $ENV_FILE"
    echo ''
    read -rp '  Press Enter to continue once you have edited .env...'
else
    step '.env already exists — skipping creation.'
fi

# ── Validate .env ─────────────────────────────────────────────────────────────
BAD_DEFAULTS=()
grep -q 'DOCVAULT_DB_PASSWORD=change-me'    "$ENV_FILE" 2>/dev/null && BAD_DEFAULTS+=(DOCVAULT_DB_PASSWORD)    || true
grep -q 'DOCVAULT_JWT_KEY=change-me'        "$ENV_FILE" 2>/dev/null && BAD_DEFAULTS+=(DOCVAULT_JWT_KEY)        || true
grep -q 'DOCVAULT_ADMIN_PASSWORD=change-me' "$ENV_FILE" 2>/dev/null && BAD_DEFAULTS+=(DOCVAULT_ADMIN_PASSWORD) || true

if [[ ${#BAD_DEFAULTS[@]} -gt 0 ]]; then
    warn 'These values are still set to the default placeholder:'
    for v in "${BAD_DEFAULTS[@]}"; do echo "    $v"; done
    echo ''
    read -rp '  Continue anyway? [y/N] ' confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || { echo '  Aborted. Update your .env and re-run install.sh.'; exit 1; }
fi

# ── Pull images ───────────────────────────────────────────────────────────────
step 'Pulling Docker images from GHCR...'
docker compose -f "$COMPOSE_FILE" "${PROFILES[@]}" pull

# ── Initialise Ollama model (first-time only) ──────────────────────────────────
if $WITH_OLLAMA; then
    step 'Pulling Ollama embedding model (this may take several minutes on first run)...'
    docker compose -f "$COMPOSE_FILE" --profile ollama run --rm ollama-init || \
        warn 'ollama-init returned an error — retry with: docker compose --profile ollama run --rm ollama-init'
fi

# ── Start services ────────────────────────────────────────────────────────────
step 'Starting DocVault...'
docker compose -f "$COMPOSE_FILE" "${PROFILES[@]}" up -d

# ── Done ──────────────────────────────────────────────────────────────────────
header 'DocVault is running!'
echo '  UI   ->  http://localhost:3000'
echo '  API  ->  http://localhost:8080'
echo ''
echo '  Your admin credentials are in .env (DOCVAULT_ADMIN_EMAIL / DOCVAULT_ADMIN_PASSWORD).'
echo ''
echo '  Useful commands:'
echo "    Stop:    docker compose -f '$COMPOSE_FILE' down"
echo "    Logs:    docker compose -f '$COMPOSE_FILE' logs -f"
echo "    Update:  ./install.sh --update"
echo ''
