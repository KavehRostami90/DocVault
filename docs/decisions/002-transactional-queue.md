# ADR-002 — Transactional Database Queue Instead of a Message Broker

**Status:** Accepted

---

## Context

When a user uploads a document, an indexing work item must be delivered to `IndexingWorker`. Three options were considered:

1. **Call `IWorkQueue.Enqueue()` after the DB transaction** (original implementation)
2. **Use a message broker** (Kafka, RabbitMQ, Azure Service Bus)
3. **Write the queue entry inside the DB transaction** (current implementation)

## Problem with Option 1

Calling enqueue *after* the transaction commits leaves a crash window:

```
COMMIT (Document + ImportJob written)
  ↓
process crashes here → IndexingWorkItem is LOST forever
  ↓
IWorkQueue.Enqueue() never called
```

`Pending` jobs would sit in the database indefinitely until the next process restart triggered startup recovery.

## Decision

Write the `IndexingQueueEntry` row **inside the same database transaction** as `Document` and `ImportJob` using `IIndexingQueueRepository`. This is a **transactional database queue** — similar in spirit to the Transactional Outbox Pattern but without a relay process or external broker.

## Why Not Kafka / RabbitMQ / Azure Service Bus?

| Factor | Broker (Kafka etc.) | Transactional DB Queue |
|---|---|---|
| **Operational cost** | Broker cluster + schema registry + monitoring | Nothing new — already have PostgreSQL |
| **Infrastructure** | Kafka (KRaft) or managed service | Zero |
| **Fan-out** | ✅ Multiple consumer groups | ❌ Single consumer |
| **Event replay** | ✅ Configurable retention | ❌ Rows deleted on dequeue |
| **Latency** | Near-zero (push) | ~500 ms poll interval |
| **Atomicity** | Requires outbox relay process | ✅ Native DB transaction |
| **Horizontal scaling** | ✅ Partition-based | ✅ `SKIP LOCKED` (PostgresWorkQueue) |
| **Right for DocVault now?** | ❌ Over-engineered | ✅ |

DocVault currently has **one producer** (upload handler) and **one consumer** (`IndexingWorker`). Introducing a broker to serve a single queue is pure overhead.

## Why Not the Full Transactional Outbox Pattern?

The [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html) adds:

- An `OutboxEvents` table (written in the same transaction ✅ — same as DocVault)
- A **relay process** that reads the outbox and publishes to a broker
- A **consumer** that reads from the broker

DocVault skips the relay and broker: `IndexingWorker` reads `IndexingQueue` directly. Same atomicity guarantee, significantly less infrastructure.

## Implementation

```csharp
await _unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    await _documents.AddAsync(document, ct);
    await _imports.AddAsync(job, ct);
    await _queue.AddAsync(workItem, ct);  // ← inside the same transaction
}, cancellationToken);
```

Two `IIndexingQueueRepository` implementations are wired by DI based on the environment:

- `EfIndexingQueueRepository` (PostgreSQL) — stages on shared `DbContext`, no `SaveChanges`; committed by the UoW
- `ChannelIndexingQueueRepository` (in-memory / dev) — delegates to `IWorkQueue<T>.Enqueue()`

## Evolution Path — Adding Kafka

When any of these are needed:
- Multiple independent consumers (analytics, notifications, re-ranking)
- Event replay / audit log
- Microservice decomposition

Migrate by:
1. Adding a `Published` column to `IndexingQueue` (no rename — it's the same table)
2. Adding a relay `BackgroundService` polling `IndexingQueue WHERE Published = false` and producing to Kafka
3. (Optional) replacing the poller with Debezium CDC for near-zero latency
4. Updating `IndexingWorker` to consume from the Kafka topic

See [`docs/background-queue.md`](../background-queue.md) for detailed migration steps.
