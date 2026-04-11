# API Reference

Base URL:

| Environment | URL |
|---|---|
| Docker | `http://localhost:8080` |
| Development | `https://localhost:<port>` |
| Production | configured via Azure App Settings |

All error responses follow RFC 7807 `application/problem+json`. Every request carries an `X-Correlation-Id` header injected by `CorrelationIdMiddleware`.

Interactive docs (available once the API is running):
- **Scalar UI**: `/scalar/v1`
- **Swagger UI**: `/swagger`
- **OpenAPI spec**: `/openapi/v1.json`

---

## Authentication

Endpoints under `/api/v1/` (except `/auth/`) require a `Bearer` token in the `Authorization` header. Obtain a token via `POST /auth/login` or `POST /auth/refresh`.

The refresh token is stored in an httpOnly cookie (`SameSite=None; Secure`). Include `credentials: 'include'` (or equivalent) in all requests so the browser sends the cookie automatically.

---

## API Versioning

All business endpoints are versioned via the URL segment:

```
/api/v{version}/...
```

The current version is **v1**. The server returns an `api-supported-versions` response header listing all available versions.

Health endpoints (`/health/live`, `/health/ready`) are infrastructure probes and are **not** versioned.

---

## Auth

### `POST /api/v1/auth/register`
Create a new `User` account.

**Body:**
```json
{ "email": "user@example.com", "password": "Secret1234!" }
```

**Response `200 OK`:** `TokenResponse`

---

### `POST /api/v1/auth/login`
Authenticate and receive tokens.

**Body:**
```json
{ "email": "user@example.com", "password": "Secret1234!" }
```

**Response `200 OK`:**
```json
{ "accessToken": "<jwt>", "expiresIn": 900 }
```

The refresh token is set as an httpOnly cookie.

---

### `POST /api/v1/auth/guest`
Create a temporary `Guest` account. The account and all its documents are automatically expired after 24 hours.

**Response `200 OK`:** `TokenResponse`

---

### `POST /api/v1/auth/refresh`
Exchange a valid refresh token cookie for a new access token. Called automatically by the frontend client on 401 responses.

**Response `200 OK`:** `TokenResponse`

---

### `POST /api/v1/auth/logout`
Revoke the current refresh token and clear the cookie.

**Response `200 OK`**

---

### `GET /api/v1/auth/me`
Return the current user's profile.

**Response `200 OK`:**
```json
{ "id": "<uuid>", "email": "user@example.com", "roles": ["User"] }
```

---

## Admin

All admin endpoints require the `Admin` role.

### `GET /api/v1/admin/users`
List all registered users.

**Response `200 OK`:** `UserSummary[]`

---

### `GET /api/v1/admin/documents`
List all documents across all users (ignores ownership filtering).

Supports the same query parameters as `GET /api/v1/documents`.

---

## Documents

### `POST /api/v1/documents/import`
Upload a document for storage and background indexing.

**Content-Type:** `multipart/form-data`

| Field | Type | Rules |
|---|---|---|
| `file` | file | Required; PDF / TXT / DOCX; 1 byte – 50 MB |
| `title` | string | Required; 1–256 characters |
| `tags` | string[] | Optional; ≤ 20 tags; 1–50 chars each; alphanumeric + `-_` |

**Response `201 Created`:**
```json
{ "id": "<uuid>", "jobId": "<uuid>" }
```
`Location` header points to `GET /api/v1/documents/{id}`.

The document is immediately stored and its `Status` is set to `Imported`. Background indexing begins asynchronously.

---

### `GET /api/v1/documents`
List documents with pagination, filtering, and sorting.

| Query param | Type | Default | Notes |
|---|---|---|---|
| `page` | int | 1 | ≥ 1 |
| `size` | int | 20 | 1–200 |
| `status` | string | — | `pending`, `imported`, `indexed`, `failed` |
| `titleFilter` | string | — | Substring match; ≤ 100 chars |
| `sort` | string | `createdAt` | `title`, `fileName`, `size`, `status`, `createdAt`, `updatedAt` |
| `sortDir` | string | `desc` | `asc`, `desc` |

**Response `200 OK`:** `PageResponse<DocumentListItem>`

---

### `GET /api/v1/documents/{id}`
Get full document details including extracted text and tags.

**Response `200 OK`:** `DocumentReadResponse`  
**Response `404 Not Found`** if the document does not exist.

---

### `PUT /api/v1/documents/{id}`
Update a document's tag list.

**Body:**
```json
{ "tags": ["finance", "q4"] }
```

**Response `200 OK`:** `DocumentReadResponse`

---

### `DELETE /api/v1/documents/{id}`
Permanently delete a document and its stored binary.

**Response `204 No Content`**

---

## Search

### `POST /api/v1/search/documents`
Full-text keyword search across document titles and extracted text.

**Body:**
```json
{
  "query": "machine learning",
  "page": 1,
  "size": 10
}
```

| Field | Rules |
|---|---|
| `query` | Required; 2–512 characters; not whitespace-only |
| `page` | ≥ 1 |
| `size` | 1–200 |

**Response `200 OK`:**
```json
{
  "items": [
    { "id": "<uuid>", "title": "…", "snippet": "…", "score": 0.87 }
  ],
  "page": 1,
  "size": 10,
  "totalCount": 42
}
```

Results are ordered by relevance score (descending). Title matches score higher than body-only matches. Snippets are at most 120 characters.

---

## Imports

### `POST /api/v1/imports`
Start an import job for an already-stored document (used internally or via admin tooling).

**Body:**
```json
{
  "documentId": "<uuid>",
  "fileName": "report.pdf",
  "storagePath": "/storage/abc.bin",
  "contentType": "application/pdf"
}
```

**Response `202 Accepted`:** `{ "jobId": "<uuid>" }`

---

### `GET /api/v1/imports/{jobId}`
Poll the status of an import job.

**Response `200 OK`:**
```json
{
  "id": "<uuid>",
  "documentId": "<uuid>",
  "status": "Completed",
  "startedAt": "2026-03-12T18:51:28Z",
  "completedAt": "2026-03-12T18:51:30Z",
  "error": null
}
```

Possible `status` values: `Pending`, `InProgress`, `Completed`, `Failed`.

---

## Tags

### `GET /api/v1/tags`
List all tag names in use across the corpus.

**Response `200 OK`:** `string[]`

---

## Health

Health endpoints do **not** appear in Swagger/OpenAPI — call them directly.

---

### `GET /health/live`
Liveness probe. Returns `200 OK` as long as the process is running. No dependency checks are performed, making it safe to use as a Kubernetes `livenessProbe` or Docker HEALTHCHECK target.

**Response `200 OK`:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.000",
  "checks": {}
}
```

---

### `GET /health/ready`
Readiness probe. Runs all checks tagged `ready` — currently **database** and **storage**. Returns `503 Service Unavailable` if any check fails. Use this as a Kubernetes `readinessProbe` to stop traffic reaching the pod until all dependencies are up.

**Response `200 OK`** (all dependencies healthy):
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.012",
  "checks": {
    "database": {
      "status": "Healthy",
      "description": null,
      "duration": "00:00:00.011",
      "error": null
    },
    "storage": {
      "status": "Healthy",
      "description": null,
      "duration": "00:00:00.001",
      "error": null
    }
  }
}
```

**Response `503 Service Unavailable`** (a dependency is down):
```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.003",
  "checks": {
    "database": {
      "status": "Unhealthy",
      "description": null,
      "duration": "00:00:05.002",
      "error": "Connection refused"
    },
    "storage": {
      "status": "Healthy",
      "description": null,
      "duration": "00:00:00.001",
      "error": null
    }
  }
}
```

#### Dependency checks

| Check | Tag | What it does |
|---|---|---|
| `database` | `ready` | Calls `CanConnectAsync` on the EF Core `DocVaultDbContext` |
| `storage` | `ready` | Writes a 1-byte probe file to `IFileStorage` and immediately deletes it |

---

## Validation Errors

All `400 Bad Request` responses return `application/problem+json`:

```json
{
  "status": 400,
  "title": "Validation failed",
  "errors": {
    "Title": ["Title must not be empty."],
    "File": ["Only PDF, TXT, and DOCX files are accepted."]
  },
  "traceId": "00-abc…"
}
```

Contracts and validators live under `src/DocVault.Api/Contracts/` and `src/DocVault.Api/Validation/` respectively. All limits are sourced from `ValidationConstants` in the Domain layer.
