# ADR-009 — Background Worker Instead of Inline Processing

**Status:** Accepted

---

## Context

When a user uploads a document, several expensive operations must happen before the document is fully searchable:

1. **Text extraction** — PDF parsing (PdfPig), DOCX parsing (OpenXml), or OCR via Tesseract (can take seconds for a scanned image)
2. **Text chunking** — splitting extracted text into overlapping 400-word windows
3. **Embedding generation** — one HTTP call to the embedding API per batch of chunks
4. **Vector persistence** — writing `DocumentChunks` rows with HNSW index maintenance

Two approaches were considered:

- **Inline processing** — do all of the above synchronously inside the upload HTTP request
- **Background worker** — persist the file and return immediately; a `BackgroundService` picks up the work

## Decision

Process documents in a **`BackgroundService` (`IndexingWorker`)** that dequeues work items from `IndexingQueue`. The upload endpoint returns as soon as the file is stored and the queue entry is written (typically < 200 ms). Indexing happens asynchronously.

## Reasoning

### 1 — Upload latency

Tesseract OCR on a dense scanned page can take 5–30 seconds. An embedding API call for a 400-chunk document can take 10+ seconds even with batching. Making users wait for all of this before receiving a `200 OK` would make uploads feel broken.

### 2 — HTTP timeouts and client disconnects

Browsers and API gateways impose default timeouts (often 30–60 seconds). A large PDF processed inline could breach these limits, leaving the client with an ambiguous error even though the document was saved.

### 3 — Back-pressure and resource control

`IndexingWorker` processes up to `MaxDegreeOfParallelism` documents concurrently (default: 2). This single knob caps simultaneous calls to the embedding API, DB connection pool usage, and CPU usage (OCR). Inline processing has no such control — a burst of uploads would spawn unbounded concurrent work.

### 4 — Retryability

If the embedding API is temporarily unavailable during inline processing, the entire upload request fails and the user must retry. With a background worker, the job sits in `Pending` state; the worker can be restarted or the admin can trigger a reindex once the service is available.

### 5 — Crash recovery

The worker recovers `InProgress` jobs on startup (see [ADR-002](002-transactional-queue.md)). Inline processing has no recovery — a crash mid-request leaves no trace.

### 6 — Graceful drain on shutdown

`IndexingWorker` tracks in-flight tasks and waits up to `DrainTimeoutSeconds` (default: 30 s) for them to complete before cancelling. This prevents jobs from being left `InProgress` on every clean shutdown. Inline processing cannot offer a graceful drain without holding open HTTP connections.

## The Upload Response

Because indexing is asynchronous, the upload endpoint returns immediately with the document in `Imported` status and an `ImportJob` ID. Clients poll `GET /imports/{jobId}` or connect to `GET /documents/{id}/status-stream` (SSE) to follow progress.

```
POST /documents  →  201 Created
  { documentId, importJobId, status: "Imported" }
                ↓
  ImportJob transitions:  Pending → InProgress → Completed / Failed
                ↓
  Document transitions:   Imported → Indexed / Failed
```

## Trade-offs

| Pro | Con |
|---|---|
| Fast upload response (< 200 ms) | Document is not immediately searchable after upload |
| No HTTP timeout risk | Client must poll or subscribe to SSE for final status |
| Controlled concurrency and back-pressure | Additional complexity (queue, worker, status tracking) |
| Retryable on infrastructure failure | Admin reindex required if worker fails permanently |
| Graceful shutdown with drain | |

## Alternatives Considered

| Alternative | Rejected because |
|---|---|
| **Inline processing** | Unacceptably slow for OCR/embedding; HTTP timeout risk; no back-pressure |
| **Fire-and-forget `Task.Run`** | Untracked tasks — crashes are silent; no back-pressure; lost on restart |
| **Separate indexing microservice** | Premature split; operational overhead (see [ADR-001](001-monolith-first.md)) |
| **Azure Functions / AWS Lambda** | Hard external dependency; complicates local development |
