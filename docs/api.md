# API Reference

Base URL:

| Environment | URL |
|---|---|
| Docker | `http://localhost:8081` |
| Development | `https://localhost:<port>` |
| Production | configured via Azure App Settings / environment variables |

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

## Documents

### `POST /api/v1/documents`
Upload a document for storage and background indexing.

**Content-Type:** `multipart/form-data`

| Field | Type | Rules |
|---|---|---|
| `file` | file | Required; PDF / DOCX / TXT / MD / PNG / JPG / …; 1 byte – 50 MB |
| `title` | string | Required; 1–256 characters |
| `tags` | string[] | Optional; ≤ 20 tags; 1–50 chars each; alphanumeric + `-_` |

**Response `201 Created`:**
```json
{ "id": "<uuid>" }
```
`Location` header points to `GET /api/v1/documents/{id}`.

The document is immediately stored and its `Status` is set to `Imported`. Background indexing (text extraction + embedding) begins asynchronously.

---

### `GET /api/v1/documents`
List documents with pagination, filtering, and sorting.

| Query param | Type | Default | Notes |
|---|---|---|---|
| `page` | int | 1 | ≥ 1 |
| `size` | int | 20 | 1–200 |
| `status` | string | — | `pending`, `imported`, `indexed`, `failed` |
| `title` | string | — | Substring match; ≤ 100 chars |
| `tag` | string | — | Filter by tag name |
| `sort` | string | `createdAt` | `title`, `fileName`, `size`, `status`, `createdAt`, `updatedAt` |
| `desc` | bool | `true` | Descending sort direction |

**Response `200 OK`:** `PageResponse<DocumentListItemResponse>`

---

### `GET /api/v1/documents/{id}`
Get full document details including extracted text and tags.

**Response `200 OK`:** `DocumentReadResponse`
**Response `404 Not Found`** if the document does not exist or belongs to another user.

---

### `GET /api/v1/documents/{id}/preview`
Stream the original file inline (for in-browser display).

**Response `200 OK`:** file stream with the document's original `Content-Type`
**Response `404 Not Found`**

---

### `GET /api/v1/documents/{id}/download`
Download the original stored file.

**Response `200 OK`:** file stream with `Content-Disposition: attachment`
**Response `404 Not Found`**

---

### `GET /api/v1/documents/{id}/extracted-text`
Return the OCR-extracted or parsed plain text for a document.

| Query param | Type | Default | Notes |
|---|---|---|---|
| `download` | bool | `false` | `true` returns `Content-Disposition: attachment` with a `.txt` filename |

**Response `200 OK`:** `text/plain; charset=utf-8`
**Response `404 Not Found`**

---

### `PUT /api/v1/documents/{id}/tags`
Replace the tag set for a document.

**Body:**
```json
{ "tags": ["finance", "q4"] }
```

**Response `204 No Content`**

---

### `DELETE /api/v1/documents/{id}`
Permanently delete a document and its stored binary.

**Response `204 No Content`**

---

## Search

### `POST /api/v1/search/documents`
Search documents by meaning (semantic) or keywords (full-text fallback).

When Ollama is available, the query is embedded as a vector and results are ranked by cosine similarity using pgvector. When Ollama is unreachable, the search falls back to PostgreSQL full-text search (`tsvector`).

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

Results are ordered by relevance score (descending). Snippets are at most 120 characters.

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
List all tag names in use across the corpus (scoped to the current user's documents; admin sees all).

**Response `200 OK`:** `string[]`

---

## Config

### `GET /api/v1/config/upload`
Return public upload limits for the client UI.

**Response `200 OK`:**
```json
{ "maxFileSizeBytes": 52428800, "maxUploadCount": 10 }
```

No authentication required.

---

## Admin

All admin endpoints require the `Admin` role.

### `GET /api/v1/admin/documents`
List all documents across all users (no ownership filtering).

Supports the same query parameters as `GET /api/v1/documents`.

**Response `200 OK`:** `PageResponse<DocumentListItemResponse>`

---

### `DELETE /api/v1/admin/documents/{id}`
Delete any document regardless of owner. Action is audit-logged.

**Response `204 No Content`**
**Response `404 Not Found`**

---

### `POST /api/v1/admin/documents/{id}/reindex`
Re-queue a document for text extraction and embedding. Allowed for any document not in `Pending` state (including `Imported`, `Indexed`, and `Failed`). Action is audit-logged.

**Response `204 No Content`**
**Response `404 Not Found`**

---

### `GET /api/v1/admin/documents/{id}/preview`
Preview any document inline, regardless of owner.

**Response `200 OK`:** file stream inline
**Response `404 Not Found`**

---

### `GET /api/v1/admin/documents/{id}/download`
Download any document, regardless of owner.

**Response `200 OK`:** file stream with `Content-Disposition: attachment`
**Response `404 Not Found`**

---

### `GET /api/v1/admin/users`
List all registered users.

**Response `200 OK`:**
```json
[
  {
    "id": "<uuid>",
    "email": "user@example.com",
    "displayName": "Alice",
    "isGuest": false,
    "createdAt": "2026-01-10T09:00:00Z",
    "roles": ["User"]
  }
]
```

---

### `DELETE /api/v1/admin/users/{id}`
Permanently delete a user account. Action is audit-logged.

**Response `204 No Content`**
**Response `404 Not Found`**
**Response `422 Unprocessable Entity`** if Identity deletion fails (e.g. last admin).

---

### `PUT /api/v1/admin/users/{id}/roles`
Replace the role set for a user. Action is audit-logged.

**Body:**
```json
{ "roles": ["Admin", "User"] }
```

**Response `204 No Content`**
**Response `404 Not Found`**
**Response `422 Unprocessable Entity`** if the role assignment fails.

---

### `GET /api/v1/admin/stats`
Return aggregate statistics about users and documents.

**Response `200 OK`:**
```json
{
  "totalUsers": 42,
  "guestUsers": 5,
  "registeredUsers": 36,
  "adminUsers": 1,
  "totalDocuments": 280,
  "documentsByStatus": {
    "Pending": 2,
    "Imported": 4,
    "Indexed": 270,
    "Failed": 4
  }
}
```

---

## Health

Health endpoints do **not** appear in Swagger/OpenAPI — call them directly.

---

### `GET /health/live`
Liveness probe. Returns `200 OK` as long as the process is running. No dependency checks are performed.

**Response `200 OK`:**
```json
{ "status": "Healthy", "totalDuration": "00:00:00.000", "checks": {} }
```

---

### `GET /health/ready`
Readiness probe. Runs all checks tagged `ready` — **database** and **storage**. Returns `503 Service Unavailable` if any check fails.

**Response `200 OK`** (all healthy):
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.012",
  "checks": {
    "database": { "status": "Healthy", "duration": "00:00:00.011", "error": null },
    "storage":  { "status": "Healthy", "duration": "00:00:00.001", "error": null }
  }
}
```

**Response `503 Service Unavailable`** (a dependency is down):
```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.003",
  "checks": {
    "database": { "status": "Unhealthy", "duration": "00:00:05.002", "error": "Connection refused" },
    "storage":  { "status": "Healthy",   "duration": "00:00:00.001", "error": null }
  }
}
```

| Check | What it does |
|---|---|
| `database` | Calls `CanConnectAsync` on the EF Core `DocVaultDbContext` |
| `storage` | Writes a 1-byte probe file to `IFileStorage` and immediately deletes it |

---

## Validation Errors

All `400 Bad Request` responses return `application/problem+json`:

```json
{
  "status": 400,
  "title": "Validation failed",
  "errors": {
    "Title": ["Title must not be empty."],
    "File": ["Only PDF, DOCX, TXT, Markdown, and image files are accepted."]
  },
  "traceId": "00-abc…"
}
```

Contracts and validators live under `src/DocVault.Api/Contracts/` and `src/DocVault.Api/Validation/` respectively. All limits are sourced from `ValidationConstants` in the Domain layer.
