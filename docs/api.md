# API Reference

Base URL (local): `http://localhost:8080`

All error responses follow RFC 7807 `application/problem+json`. Every request carries an `X-Correlation-Id` header injected by `CorrelationIdMiddleware`.

Interactive docs:
- **Scalar UI**: `/scalar/v1`
- **Swagger UI**: `/swagger`
- **OpenAPI spec**: `/openapi/v1.json`

---

## Documents

### `POST /documents/import`
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
`Location` header points to `GET /documents/{id}`.

The document is immediately stored and its `Status` is set to `Imported`. Background indexing begins asynchronously.

---

### `GET /documents`
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

### `GET /documents/{id}`
Get full document details including extracted text and tags.

**Response `200 OK`:** `DocumentReadResponse`  
**Response `404 Not Found`** if the document does not exist.

---

### `PUT /documents/{id}`
Update a document's tag list.

**Body:**
```json
{ "tags": ["finance", "q4"] }
```

**Response `200 OK`:** `DocumentReadResponse`

---

### `DELETE /documents/{id}`
Permanently delete a document and its stored binary.

**Response `204 No Content`**

---

## Search

### `POST /search/documents`
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

### `POST /imports`
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

### `GET /imports/{jobId}`
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

### `GET /tags`
List all tag names in use across the corpus.

**Response `200 OK`:** `string[]`

---

## Health

### `GET /health/live`
Liveness probe. Returns `200 OK` if the process is running.

### `GET /health/ready`
Readiness probe. Returns `200 OK` if the database is reachable.

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
