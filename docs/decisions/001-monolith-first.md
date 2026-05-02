# ADR-001 — Monolith-First Architecture

**Status:** Accepted

---

## Context

DocVault needs a clear architectural style from the start. The two main options are:

- **Microservices** — each capability (upload, indexing, search, QA, auth) is a separate deployable service communicating over a network
- **Monolith** — all capabilities live in one deployable unit, split into logical layers internally

## Decision

Start as a **monolith**, structured with Clean Architecture + CQRS so that it can be decomposed into services later if the need arises.

## Reasoning

1. **Operational simplicity.** One process, one database, one deployment pipeline. No service mesh, no distributed tracing, no inter-service auth to configure before the first feature works.

2. **Premature distribution is expensive.** Microservices introduce network latency, distributed transactions, and eventual consistency *by default*. These costs are only justified when a team or scaling problem actually forces them.

3. **Clean Architecture already gives the separation.** `Domain → Application → Infrastructure → Api` with strict dependency rules means each layer could become a separate service by moving the interfaces across a network boundary. The seam is already there.

4. **Single PostgreSQL database covers all needs.** Full-text search (`tsvector`), vector search (`pgvector`), the work queue (`IndexingQueue`), and transactional writes all live in one place — no coordination across stores.

## Trade-offs

| Pro | Con |
|---|---|
| Simple deployment (Docker Compose, single container) | Cannot scale individual capabilities independently |
| Full ACID transactions across the entire domain | One bad dependency can block the whole process |
| Easy local development (one `dotnet run`) | Team scaling becomes harder as the codebase grows |
| No distributed tracing needed yet | Feature teams cannot deploy independently |

## Evolution Path

When independent scaling or team autonomy is needed, the seams are already cut:

- `IndexingWorker` + `IngestionPipeline` → **Indexing Service** (consume from Kafka, see [ADR-002](002-transactional-queue.md))
- `SearchDocumentsHandler` + `AskQuestionHandler` → **Search & QA Service**
- `AuthEndpoints` + Identity → **Auth Service**

The `IWorkQueue`, `IFileStorage`, `IEmbeddingProvider`, and `IDomainEventDispatcher` interfaces are the natural split points.
