# Data Model

## Aggregates

### `Document`

Core aggregate representing a stored file and its extracted content.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | `DocumentId` value object wrapping a GUID |
| `Title` | `text` | 1–256 characters; required |
| `FileName` | `text` | No path traversal (`..`, `/`, `\`) |
| `ContentType` | `text` | `text/plain`, `application/pdf`, `application/vnd.openxmlformats…` |
| `Size` | `bigint` | 1 byte – 50 MB |
| `Hash` | `text` | SHA-256 hex; unique constraint |
| `Text` | `text` | Extracted plain text; populated by `IndexingWorker` |
| `Status` | `int` | `Pending=0`, `Imported=1`, `Indexed=2`, `Failed=3` |
| `IndexingError` | `text?` | Populated when `Status = Failed` |
| `CreatedAt` | `timestamptz` | Set on insert; never updated |
| `UpdatedAt` | `timestamptz` | Bumped on every `Touch()` |

**Status transitions:**
```
Pending → Imported (MarkImported → raises DocumentImported event)
        → Indexed  (MarkIndexed  → raises DocumentIndexed event)
        → Failed   (MarkFailed(error?) → stores IndexingError)
```

### `Tag`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | |
| `Name` | `text` | 1–50 chars; lowercase alphanumeric + hyphens/underscores |

Many-to-many with `Document` via the `DocumentTags` join table. A document may have at most 20 tags.

### `ImportJob`

Process ticket for background indexing. Linked to the `Document` it is indexing.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | |
| `DocumentId` | `uuid` | FK to `Documents.Id`; required |
| `FileName` | `text` | Original upload file name |
| `StoragePath` | `text` | Path within `IFileStorage` to the raw binary |
| `ContentType` | `text` | MIME type used to select the correct `ITextExtractor` |
| `Status` | `int` | `Pending=0`, `InProgress=1`, `Completed=2`, `Failed=3` |
| `StartedAt` | `timestamptz` | Set at construction |
| `CompletedAt` | `timestamptz?` | Set by `MarkCompleted()` and `MarkFailed()` |
| `Error` | `text?` | Exception message on failure |

**Status transitions:**
```
Pending → InProgress → Completed
                     → Failed (stores Error)
```

### `IndexingQueueEntries` (PostgresWorkQueue)

Durable work queue backing table used in multi-instance deployments instead of `ChannelWorkQueue<T>`.

| Column | Type | Notes |
|---|---|---|
| `Id` | `bigserial` | |
| `JobId` | `uuid` | |
| `StoragePath` | `text` | |
| `ContentType` | `text` | |
| `CreatedAt` | `timestamptz` | |

Dequeue uses `SELECT … FOR UPDATE SKIP LOCKED` for safe concurrent consumption.

## Domain Events

| Event | Raised by | Payload |
|---|---|---|
| `DocumentImported` | `Document.MarkImported()` | `DocumentId` |
| `DocumentIndexed` | `Document.MarkIndexed()` | `DocumentId` |
| `SearchExecuted` | `SearchDocumentsHandler` | query, result count |

Events are collected on the aggregate (`AggregateRoot<TId>.DomainEvents`) and dispatched via `IDomainEventDispatcher` after the unit-of-work commits.

## Database Migrations

Migrations live in `src/DocVault.Infrastructure/Migrations/`. Apply with:

```bash
# Ensure the db container is running first
docker compose up -d db

export DOCVAULT_DB="Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault"
dotnet ef database update \
  --project src/DocVault.Infrastructure \
  --startup-project src/DocVault.Api
```
