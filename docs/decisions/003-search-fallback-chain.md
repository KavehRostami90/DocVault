# ADR-003 — Search Fallback Chain (Hybrid → Vector → FTS → In-Memory)

**Status:** Accepted

---

## Context

DocVault supports two fundamentally different search modes:

- **Semantic / vector search** — requires an embedding model (Ollama, OpenAI) and the `pgvector` extension; produces ranking by meaning, not keywords
- **Full-text search (FTS)** — pure PostgreSQL `tsvector`; no AI dependency; ranking by term frequency

The embedding model is an **optional external service**. If it is unavailable (no API key, Ollama not running, network failure), search must still work.

## Decision

Implement search as a **chain of responsibility** — a prioritised list of strategies, each with a `CanHandle()` guard. The first strategy whose conditions are met wins:

```
HybridSearchStrategy        ← PostgreSQL + embedding available + text terms present
PgvectorSearchStrategy      ← PostgreSQL + embedding available (no text terms)
PostgresSearchStrategy      ← PostgreSQL, no embedding (pure FTS fallback)
InMemorySearchStrategy      ← in-memory DB (tests / no PostgreSQL)
```

## Reasoning

### 1 — No hard dependency on AI

Making semantic search optional means:

- Developers without Ollama can run the full application
- CI / integration tests run without any AI infrastructure
- A production outage of the embedding service degrades search quality, not availability

### 2 — Graceful degradation, not silent failure

If `IEmbeddingProvider.EmbedAsync` throws, `SearchDocumentsHandler` catches it, sets `queryVector = null`, logs a warning, and the chain falls to `PostgresSearchStrategy`. The user gets keyword results. The response header `X-Search-Mode` signals which mode was used so the UI can show a badge.

### 3 — Hybrid RRF is the best of both worlds

When both the embedding model and PostgreSQL are available, `HybridSearchStrategy` fuses vector ANN results with FTS results via **Reciprocal Rank Fusion (RRF, K=60)**. This consistently outperforms either method alone — vector search finds semantically related content; FTS rewards exact term matches.

### 4 — HNSW index safety rule

The HNSW index only fires when the innermost query driving `ORDER BY <=> @embedding` has no JOINs or WHERE clauses. Both vector strategies isolate the ANN pass in a clean `vec_candidates` CTE before any ownership filtering to guarantee the index is used.

## Trade-offs

| Pro | Con |
|---|---|
| Works with zero AI infrastructure | Results are lower quality without embeddings |
| Tested end-to-end without Ollama | Strategy selection logic must be maintained |
| Single search endpoint, transparent fallback | Response quality varies by environment |
| Hybrid RRF improves recall vs either strategy alone | RRF adds an extra CTE and sort pass |

## Alternatives Considered

| Alternative | Rejected because |
|---|---|
| **Require embeddings** — 503 if unavailable | Breaks dev workflow, CI, and production availability |
| **Always FTS only** | Significantly worse recall on natural language queries |
| **Elasticsearch / Azure AI Search** | Another external service; PostgreSQL already provides both FTS and vector search |
| **Single fixed strategy** | Cannot adapt to environment; requires code changes to switch modes |
