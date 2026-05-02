# ADR-008 — PostgreSQL + pgvector Instead of Elasticsearch

**Status:** Accepted

---

## Context

DocVault needs both **full-text search** (keyword ranking) and **vector similarity search** (semantic/embedding-based ranking). The two leading options are:

- **Elasticsearch** — mature FTS engine; recently added native dense-vector search (`knn` queries)
- **PostgreSQL + pgvector** — relational DB already used for all other data; `pgvector` adds HNSW-indexed cosine similarity

## Decision

Use **PostgreSQL 16 with the `pgvector` extension** for both FTS and vector search. Elasticsearch is not introduced.

## Why Not Elasticsearch?

### Operational cost

| Concern | Elasticsearch | PostgreSQL + pgvector |
|---|---|---|
| Services to operate | 2 (Postgres + ES) | 1 |
| Data to keep in sync | Document rows + ES index | Single source of truth |
| Backup | Postgres backup + ES snapshot | One backup target |
| Dev setup | `docker compose` + ES container | Single `pgvector/pgvector:pg16` image |
| Managed cloud cost | ES cluster (≥ 2 nodes) | One PostgreSQL Flexible instance |

Introducing Elasticsearch doubles infrastructure without providing capabilities DocVault currently needs.

### Consistency

With Elasticsearch as a secondary store, every write to `Documents` would need to be propagated to the ES index — adding eventual consistency, indexing lag, and a failure mode where search results are stale. With pgvector, the embedding is stored in the same transaction as the document metadata.

### Vector search quality at DocVault's scale

| Scale | pgvector HNSW | Elasticsearch kNN |
|---|---|---|
| Tens of thousands of chunks | < 10 ms latency | Similar |
| Millions of chunks | 50–200 ms | Similar |
| Tens of millions of chunks | May require partitioning | Scales better with sharding |

At DocVault's current and near-future scale, pgvector HNSW is fully adequate. The `DocumentChunks` table has a dedicated `vector_cosine_ops` HNSW index. Queries are isolated in a clean `vec_candidates` CTE to guarantee the planner uses the index (see [ADR-003](003-search-fallback-chain.md)).

### Hybrid search in one query

The hybrid `HybridSearchStrategy` fuses vector ANN results with `tsvector` FTS results via Reciprocal Rank Fusion entirely in SQL — no cross-service call, no serialisation overhead, consistent results within a single DB transaction.

## Trade-offs

| Pro | Con |
|---|---|
| Zero extra infrastructure | Not the best FTS scorer for multilingual, faceted, or highly tuned relevance |
| Single source of truth — no sync lag | HNSW build quality lags behind purpose-built vector DBs at extreme scale |
| Hybrid search in one SQL query | ES has richer query DSL (percolation, significant terms, etc.) |
| Transactional writes — embedding committed with document | |
| pgvector actively developed; HNSW added in 0.5.0 | |

## When to Reconsider

| Trigger | Alternative |
|---|---|
| Vector search latency > 200 ms at scale | pgvector on a dedicated read replica, or Qdrant / Pinecone |
| FTS needs language-specific analyzers, facets, or auto-suggest | Elasticsearch or Azure AI Search |
| Full-text ranking must be tuned beyond `ts_rank` | Elasticsearch BM25 with custom field boosts |

## Pluggable Boundary

`IDocumentRepository.SearchAsync` accepts a list of `IDocumentSearchStrategy` implementations. Adding an `ElasticsearchSearchStrategy` later requires no changes to the domain or application layers — only a new Infrastructure class and a DI registration.
