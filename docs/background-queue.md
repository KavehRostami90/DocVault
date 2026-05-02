# Background Job Queue — Design & Evolution

This document explains the design of DocVault's background indexing queue: the problem it solves, the implementation chosen, why it differs from the full Transactional Outbox Pattern, and the migration path to Kafka when the need arises.

---

## The Problem — Split Transaction

When a user uploads a document, three things must happen atomically:

1. A `Document` row is written (status `Imported`)
2. An `ImportJob` row is written (status `Pending`)
3. A work item is enqueued so `IndexingWorker` picks it up

The original implementation committed the DB transaction first, then called `IWorkQueue.Enqueue()` — two separate operations:

```
BEGIN TRANSACTION
  INSERT Documents
  INSERT ImportJobs
COMMIT
↓
IWorkQueue.Enqueue()  ← if the process crashes here, the work item is LOST
```

This left a crash window where `ImportJob` rows existed in the database with `Pending` status but no corresponding queue entry. The only recovery was a full process restart that triggered startup scanning. In a long-running production process this could delay or silently drop indexing work.

---

## The Fix — Transactional Queue

The `IndexingQueueEntry` row is now written **inside the same database transaction** as `Document` and `ImportJob`:

```
BEGIN TRANSACTION
  INSERT Documents
  INSERT ImportJobs
  INSERT IndexingQueue  ← atomic with the business rows
COMMIT
↓
(nothing more to do — the queue row is already durable)
```

If any of the three inserts fail, the transaction rolls back and none are persisted. There is no window where a job exists without a queue entry.

### Interface

```csharp
// DocVault.Application.Abstractions.Persistence
public interface IIndexingQueueRepository
{
    // Must NOT call SaveChanges — committed by the surrounding UoW transaction.
    Task AddAsync(IndexingWorkItem item, CancellationToken cancellationToken = default);
}
```

### Implementations

| Implementation | When used | Behaviour |
|---|---|---|
| `EfIndexingQueueRepository` | PostgreSQL / production | Calls `DbContext.IndexingQueue.AddAsync()` — stages the entity on the shared `DbContext` without flushing. The UoW transaction commits it together with `Document` and `ImportJob`. |
| `ChannelIndexingQueueRepository` | In-memory / dev / tests | Delegates to `IWorkQueue<T>.Enqueue()`. The in-memory channel is the queue — no DB row is needed. |

### DI wiring (automatic)

`DependencyInjection.cs` registers the correct implementation based on whether a database connection string is present:

```csharp
if (hasConnectionString)
{
    // Postgres path — dequeue via SKIP LOCKED, enqueue via EF (atomic)
    services.AddSingleton<IWorkQueue<IndexingWorkItem>, PostgresWorkQueue>();
    services.AddScoped<IIndexingQueueRepository, EfIndexingQueueRepository>();
}
else
{
    // In-memory path — dequeue via Channel<T>, enqueue directly to channel
    services.AddSingleton<IWorkQueue<IndexingWorkItem>, ChannelWorkQueue<IndexingWorkItem>>();
    services.AddScoped<IIndexingQueueRepository, ChannelIndexingQueueRepository>();
}
```

---

## Crash Recovery

| Job status | Queue row state | Recovery action |
|---|---|---|
| `Pending` | Row is in `IndexingQueue` (transactional guarantee) | None needed — worker will dequeue normally |
| `InProgress` | Queue row was consumed by `DequeueAsync` before crash | `IndexingWorker` re-inserts via `IWorkQueue.Enqueue()` on startup |
| `Completed` | No queue row | None |
| `Failed` | No queue row | Admin can trigger reindex via `POST /admin/documents/{id}/reindex` |

`IndexingWorker.RecoverPendingJobsAsync()` calls `IImportJobRepository.GetInProgressAsync()` — only `InProgress` jobs are re-queued, because `Pending` jobs always have a durable queue row after the fix.

---

## Architecture Comparison

### What DocVault uses — Transactional Database Queue

```
┌─ same DB transaction ──────────┐
│  INSERT Documents               │
│  INSERT ImportJobs              │
│  INSERT IndexingQueue           │
└────────────────────────────────┘
         │
  IndexingWorker (BackgroundService)
  reads IndexingQueue directly
  (SELECT … FOR UPDATE SKIP LOCKED)
```

**Pros:** simple, zero extra infrastructure, works in a single process or multi-instance with `PostgresWorkQueue`.

**Cons:** no fan-out (only one consumer), no message replay, polling every 500 ms.

---

### Full Transactional Outbox Pattern

The [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html) adds a relay process and an external broker:

```
┌─ same DB transaction ──────────────────┐
│  INSERT Documents                       │
│  INSERT ImportJobs                      │
│  INSERT IndexingQueue { Published=false}│
└────────────────────────────────────────┘
         │
   Outbox Relay (separate process / Debezium CDC)
   reads IndexingQueue WHERE Published = false
         │
   Kafka / RabbitMQ / Azure Service Bus
         │
   IndexingWorker (consumer group)
```

**Pros:** fan-out to multiple consumers, event replay, decoupled microservices.

**Cons:** requires a broker cluster + relay process; more operational overhead.

---

## Migration Path — Adding Kafka

When DocVault needs multiple consumers or event replay, the migration steps are:

### Step 1 — Add `Published` column to `IndexingQueue`

Add a migration that adds a `Published` boolean column (no rename needed — it's the same table):

```sql
ALTER TABLE "IndexingQueue" ADD COLUMN "Published" boolean NOT NULL DEFAULT false;
```

### Step 2 — Implement an outbox relay

Two options for relaying `IndexingQueue` rows to Kafka:

- **Custom poller** — a new `BackgroundService` that reads `IndexingQueue WHERE Published = false` every 200–500 ms, produces each row to the Kafka topic, then sets `Published = true`. Low infrastructure cost; adds ~500 ms latency.
- **Debezium CDC** — reads the Postgres WAL directly and publishes row-insert events to Kafka automatically. Sub-millisecond latency, no polling load, but requires a Kafka Connect cluster.

### Step 3 — Update `IndexingWorker` to consume from Kafka

```csharp
// Replace IWorkQueue.DequeueAsync with a Kafka consumer
// using Confluent.Kafka IConsumer<string, IndexingWorkItem>
```

### Step 4 — Remove `IWorkQueue` / `ChannelWorkQueue` / `PostgresWorkQueue`

Once all enqueue and dequeue paths go through Kafka, the internal queue abstractions can be retired.

---

## Current Known Limitations (not blocking, noted for future work)

| Gap | Impact | Mitigation |
|---|---|---|
| No retry count / backoff | A bad document immediately goes to `Failed`; must be manually reindexed | Low — admin reindex is available |
| `PostgresWorkQueue` polls every 500 ms | Slight latency; small load on DB | Acceptable for current scale; replace with LISTEN/NOTIFY or Kafka for sub-100 ms |
| `ChannelWorkQueue` is single-instance | Items lost on restart in dev | Expected — dev mode only |
