# Architecture Decision Records

This folder documents the key design decisions made in DocVault — what was decided, why, what alternatives were rejected, and when the decision should be revisited.

Each ADR is a standalone document. New decisions should be added as new numbered files.

---

| # | Decision | Status |
|---|---|---|
| [001](001-monolith-first.md) | Monolith-First Architecture | Accepted |
| [002](002-transactional-queue.md) | Transactional Database Queue Instead of a Message Broker | Accepted |
| [003](003-search-fallback-chain.md) | Search Fallback Chain (Hybrid → Vector → FTS → In-Memory) | Accepted |
| [004](004-result-type-error-handling.md) | Result\<T\> for Expected Failures, Not Exceptions | Accepted |
| [005](005-chunk-level-embeddings.md) | Chunk-Level Embeddings Instead of Document-Level | Accepted |
| [006](006-jwt-cookie-token-split.md) | JWT Access Token in sessionStorage + Refresh Token in httpOnly Cookie | Accepted |
| [007](007-postgresql-for-everything.md) | PostgreSQL for Queue, Vector Search, and Full-Text Search | Accepted |
| [008](008-pgvector-vs-elasticsearch.md) | PostgreSQL + pgvector Instead of Elasticsearch | Accepted |
| [009](009-background-worker-vs-inline.md) | Background Worker Instead of Inline Processing | Accepted |
| [010](010-embedding-batching.md) | Embedding Batching (O(n) → O(1) HTTP Round-Trips) | Accepted |

---

## ADR Format

Each record contains:

- **Context** — the situation and forces at play when the decision was made
- **Decision** — what was decided, stated clearly
- **Reasoning** — why this option was chosen
- **Trade-offs** — what you gain and what you give up
- **Alternatives Considered** — other options and why they were rejected
- **Evolution Path** — when and how to revisit the decision
