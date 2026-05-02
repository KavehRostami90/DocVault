# ADR-007 — PostgreSQL for Queue, Vector Search, and Full-Text Search

**Status:** Accepted

---

## Context

DocVault needs four distinct data capabilities:

1. **Relational storage** — documents, users, jobs, tags
2. **Vector similarity search** — nearest-neighbour over 768-dimension embeddings
3. **Full-text search** — keyword ranking over document text
4. **Work queue** — durable, crash-safe background job queue

The specialist-tool approach would use Elasticsearch (FTS + vector), Redis (queue), and PostgreSQL (relational). Alternatively, PostgreSQL can handle all four with extensions.

## Decision

Use **PostgreSQL 16 with the `pgvector` extension** for all four capabilities. No additional data infrastructure is introduced.

## Reasoning

### 1 — pgvector handles vector search at DocVault's scale

`pgvector` supports HNSW indexing with cosine distance. At tens of thousands of document chunks, HNSW latency is well under 100 ms. Dedicated vector databases (Pinecone, Weaviate, Qdrant) are only justified at tens of millions of vectors with sub-10 ms SLAs.

### 2 — PostgreSQL FTS is production-grade

`tsvector` / `tsquery` with GIN indexes and `ts_rank` scoring handles the FTS requirements without Elasticsearch. The hybrid search strategy (RRF over vector + FTS) runs in a single SQL query — no cross-service call needed.

### 3 — The work queue benefits from the same transaction

Using PostgreSQL for the queue (`IndexingQueue` table) means the queue entry is written in **the same database transaction** as the business rows (see [ADR-002](002-transactional-queue.md)). This atomicity is impossible when the queue is a separate service (Redis, RabbitMQ) without a distributed transaction coordinator.

### 4 — Operational simplicity

One database to back up, monitor, and scale. One `docker run` in development. One managed service in production (Azure Database for PostgreSQL Flexible, Amazon RDS, Supabase).

### 5 — Full ACID integrity across all writes

`Document`, `ImportJob`, `IndexingQueue`, `DocumentChunks`, and `RefreshTokens` all participate in the same ACID transactions. Cross-entity consistency is free — no saga patterns or compensating transactions.

## Trade-offs

| Pro | Con |
|---|---|
| Zero extra infrastructure | Not the fastest vector DB at extreme scale (>10 M vectors) |
| Transactional enqueue (ADR-002) | HNSW build is less optimised than dedicated vector DBs |
| Single backup / monitoring target | FTS ranking is less tunable than Elasticsearch's BM25 |
| Hybrid search in one SQL query | Requires careful query shaping to keep the HNSW index active |

## When to Reconsider

| Trigger | Alternative |
|---|---|
| Vector search latency > 200 ms at scale | Pinecone, Weaviate, Qdrant, or pgvector on a read replica |
| FTS needs facets, custom analyzers, or multilingual stemming | Elasticsearch or Azure AI Search |
| Queue needs fan-out, replay, or cross-service pub/sub | Kafka (see [ADR-002](002-transactional-queue.md)) |
| Hot read paths need sub-millisecond caching | Redis (already abstracted behind `ISearchResultCache`) |

## Pluggable Abstractions

All capabilities sit behind interfaces — a specialist tool can be swapped in without touching domain or application logic:

| Interface | Current implementation | Alternative |
|---|---|---|
| `IDocumentRepository.SearchAsync` | `HybridSearchStrategy` / `PgvectorSearchStrategy` / `PostgresSearchStrategy` | Elasticsearch strategy |
| `IWorkQueue<T>` | `PostgresWorkQueue` | Kafka consumer |
| `ISearchResultCache` | In-memory `MemoryCache` | Redis |
| `IFileStorage` | `LocalFileStorage` / `AzureBlobFileStorage` | AWS S3, MinIO |
