<#
.SYNOPSIS
    Install and start DocVault on Windows.

.DESCRIPTION
    Checks prerequisites, sets up .env, pulls Docker images from GHCR,
    and starts all services with docker compose.

.PARAMETER WithOllama
    Also start the containerised Ollama service (embedding + QA).
    On first run the init container will pull the embedding model automatically.

.PARAMETER Update
    Pull the latest images and restart services (upgrade in-place).

.PARAMETER Down
    Stop and remove containers (data volumes are preserved).

.EXAMPLE
    .\install.ps1
    .\install.ps1 -WithOllama
    .\install.ps1 -Update
    .\install.ps1 -Down
#>
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [switch]$WithOllama,
    [Parameter(ParameterSetName = 'Update')] [switch]$Update,
    [Parameter(ParameterSetName = 'Down')]   [switch]$Down
)

$ErrorActionPreference = 'Stop'

$ScriptDir   = $PSScriptRoot
$ComposeFile = Join-Path $ScriptDir 'docker-compose.yml'
$EnvExample  = Join-Path $ScriptDir '.env.example'
$EnvFile     = Join-Path $ScriptDir '.env'

function Write-Header([string]$Text) {
    Write-Host ""
    Write-Host "  === $Text ===" -ForegroundColor Cyan
    Write-Host ""
}
function Write-Step([string]$Text) { Write-Host "  >>  $Text" -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host "  **  $Text" -ForegroundColor Yellow }
function Write-Fail([string]$Text) { Write-Host "  !!  $Text" -ForegroundColor Red }

# Returns a value from .env by key, or a default if not present.
function Get-EnvValue([string]$Key, [string]$Default = '') {
    $line = (Get-Content $EnvFile -ErrorAction SilentlyContinue) |
        Where-Object { $_ -match "^\s*$Key\s*=\s*(.+)\s*$" } |
        Select-Object -First 1
    if ($line) { return ($line -split '=', 2)[1].Trim() }
    return $Default
}

# Polls /health/ready until the API responds 200 or times out.
function Wait-ApiHealthy([string]$Port) {
    $url         = "http://localhost:$Port/health/ready"
    $maxAttempts = 24
    for ($i = 1; $i -le $maxAttempts; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            if ($r.StatusCode -eq 200) { return $true }
        } catch { }
        Write-Host "  Waiting for API... ($i/$maxAttempts)" -ForegroundColor Gray
        Start-Sleep -Seconds 5
    }
    return $false
}

$Profiles = if ($WithOllama) { @('--profile', 'ollama') } else { @() }

# ── Down ──────────────────────────────────────────────────────────────────────
if ($Down) {
    Write-Header 'Stopping DocVault'
    docker compose -f $ComposeFile @Profiles down
    Write-Host '  Services stopped. Data volumes are preserved.' -ForegroundColor Gray
    exit 0
}

Write-Header 'DocVault Installer'

# ── Check Docker ──────────────────────────────────────────────────────────────
Write-Step 'Checking prerequisites...'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Fail 'Docker is not installed.'
    Write-Host '  Download Docker Desktop: https://docs.docker.com/desktop/install/windows-install/' -ForegroundColor White
    exit 1
}

docker info 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Fail 'Docker daemon is not running. Please start Docker Desktop and try again.'
    exit 1
}

# Require Docker Engine >= 20 (compose v2 is bundled from that version on).
$dockerVersion = (docker version --format '{{.Server.Version}}' 2>$null) -replace '^(\d+).*', '$1'
if ([int]$dockerVersion -lt 20) {
    Write-Fail "Docker Engine $dockerVersion is too old. Please upgrade to version 20 or later."
    exit 1
}

Write-Step "Docker OK  ($(docker --version))"

# ── Update path ───────────────────────────────────────────────────────────────
if ($Update) {
    Write-Step 'Pulling latest images...'
    docker compose -f $ComposeFile @Profiles pull
    if ($LASTEXITCODE -ne 0) { Write-Fail 'Image pull failed.'; exit 1 }

    Write-Step 'Restarting services...'
    docker compose -f $ComposeFile @Profiles up -d
    if ($LASTEXITCODE -ne 0) { Write-Fail 'docker compose up failed.'; exit 1 }

    $ApiPort = Get-EnvValue 'DOCVAULT_API_PORT' '8080'
    $UiPort  = Get-EnvValue 'DOCVAULT_UI_PORT'  '3000'

    Write-Step 'Waiting for API to be ready...'
    if (-not (Wait-ApiHealthy $ApiPort)) {
        Write-Warn 'API did not become healthy within 2 minutes. Check logs: docker compose logs api'
    }

    Write-Header 'DocVault updated successfully'
    Write-Host "  UI   ->  http://localhost:$UiPort" -ForegroundColor White
    Write-Host "  API  ->  http://localhost:$ApiPort" -ForegroundColor White
    exit 0
}

# ── Configure .env ────────────────────────────────────────────────────────────
if (-not (Test-Path $EnvFile)) {
    Write-Step 'Creating .env from .env.example...'
    Copy-Item $EnvExample $EnvFile
    Write-Warn 'Edit .env and set your passwords and API keys before continuing.'
    Write-Host ''
    Write-Host '  Press Enter to open .env in Notepad, or Ctrl+C to abort and edit manually.' -ForegroundColor White
    Read-Host
    Start-Process notepad -ArgumentList $EnvFile -Wait -NoNewWindow:$false
    Write-Host ''
    Read-Host '  Press Enter once you have saved .env to continue'
} else {
    Write-Step '.env already exists — skipping creation.'
}

# ── Validate .env ─────────────────────────────────────────────────────────────
$envRaw     = Get-Content $EnvFile -Raw
$badDefaults = @()
# (?m) enables multiline mode so ^ matches the start of each line, not just the
# start of the whole string. The pattern intentionally uses "change-me" as a
# prefix so it catches both the short placeholder and the long default from
# .env.example (change-me-to-a-long-random-secret-at-least-32-chars).
if ($envRaw -match '(?m)^DOCVAULT_DB_PASSWORD=change-me')    { $badDefaults += 'DOCVAULT_DB_PASSWORD' }
if ($envRaw -match '(?m)^DOCVAULT_JWT_KEY=change-me')        { $badDefaults += 'DOCVAULT_JWT_KEY' }
if ($envRaw -match '(?m)^DOCVAULT_ADMIN_PASSWORD=change-me') { $badDefaults += 'DOCVAULT_ADMIN_PASSWORD' }

if ($badDefaults.Count -gt 0) {
    Write-Warn 'These values are still set to the default placeholder:'
    $badDefaults | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    $confirm = Read-Host '  Continue anyway? [y/N]'
    if ($confirm -notmatch '^[Yy]$') {
        Write-Host '  Aborted. Update your .env and re-run install.ps1.' -ForegroundColor Red
        exit 1
    }
}

# ── Pull images ───────────────────────────────────────────────────────────────
Write-Step 'Pulling Docker images from GHCR...'
docker compose -f $ComposeFile @Profiles pull
if ($LASTEXITCODE -ne 0) { Write-Fail 'Image pull failed.'; exit 1 }

# ── Initialise Ollama model (first-time only) ─────────────────────────────────
if ($WithOllama) {
    Write-Step 'Pulling Ollama embedding model (this may take several minutes on first run)...'
    docker compose -f $ComposeFile --profile ollama run --rm ollama-init
    if ($LASTEXITCODE -ne 0) {
        Write-Warn 'ollama-init returned an error — retry with: docker compose --profile ollama run --rm ollama-init'
    }
}

# ── Start services ────────────────────────────────────────────────────────────
Write-Step 'Starting DocVault...'
docker compose -f $ComposeFile @Profiles up -d
if ($LASTEXITCODE -ne 0) { Write-Fail 'docker compose up failed.'; exit 1 }

$ApiPort = Get-EnvValue 'DOCVAULT_API_PORT' '8080'
$UiPort  = Get-EnvValue 'DOCVAULT_UI_PORT'  '3000'

Write-Step 'Waiting for API to be ready...'
if (Wait-ApiHealthy $ApiPort) {
    Write-Header 'DocVault is running!'
} else {
    Write-Header 'DocVault started (API health check timed out)'
    Write-Warn 'The API may still be starting. Check logs: docker compose logs api'
}

Write-Host "  UI   ->  http://localhost:$UiPort" -ForegroundColor White
Write-Host "  API  ->  http://localhost:$ApiPort" -ForegroundColor White
Write-Host ''
Write-Host '  Your admin credentials are in .env (DOCVAULT_ADMIN_EMAIL / DOCVAULT_ADMIN_PASSWORD).' -ForegroundColor Gray
Write-Host ''
Write-Host '  Useful commands:' -ForegroundColor Gray
Write-Host "    Stop:    docker compose -f `"$ComposeFile`" down" -ForegroundColor Gray
Write-Host "    Logs:    docker compose -f `"$ComposeFile`" logs -f" -ForegroundColor Gray
Write-Host "    Update:  .\install.ps1 -Update" -ForegroundColor Gray
Write-Host ''
