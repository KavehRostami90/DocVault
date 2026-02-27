# System Design

- Monolith-first, vertical slices per feature.
- PostgreSQL via EF Core 10 for persistence.
- Ingestion pipeline stages: file read -> text extraction -> embeddings -> index/search.
- Background worker dequeues indexing jobs from in-memory queue (replaceable).
- Minimal APIs with pagination/filtering contracts.
