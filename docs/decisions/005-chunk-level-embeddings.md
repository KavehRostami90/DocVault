# ADR-005 — Chunk-Level Embeddings Instead of Document-Level

**Status:** Accepted

---

## Context

When indexing a document for vector search, two approaches exist:

1. **Document-level embedding** — embed the entire extracted text as a single vector
2. **Chunk-level embedding** — split the text into overlapping windows, embed each chunk separately, store one vector per chunk

## Decision

Use **chunk-level embeddings** with 400-word windows and 80-word overlap, stored in a dedicated `DocumentChunks` table. Each chunk carries `StartChar`/`EndChar` offsets back into the original extracted text.

## Reasoning

### 1 — Embedding models have a context window

Most embedding models (including `nomic-embed-text`) are trained on sequences of a few hundred to a few thousand tokens. Feeding a 50-page PDF as a single string truncates or averages away the signal — the resulting vector is too diffuse to match specific queries.

### 2 — Long documents contain multiple topics

A technical manual may cover installation in chapter 1, API reference in chapter 2, and troubleshooting in chapter 3. A single document-level embedding averages all three topics together. A query about "installation" should match the relevant section, not the entire document.

### 3 — Chunk offsets enable precise snippet retrieval

Each `DocumentChunk` stores `StartChar` and `EndChar` into the extracted text. When a vector search returns the best matching chunk, the API surfaces the exact passage — not a random excerpt from elsewhere in the document.

### 4 — HNSW index works best on many focused vectors

The pgvector HNSW index builds a graph of nearest neighbours. Many smaller, topically focused vectors produce a denser, more useful graph than a few large, blended vectors.

### 5 — No re-chunking at query time

Chunks are computed once during ingestion and stored permanently. The RAG pipeline fetches pre-indexed chunks directly at query time — no re-splitting or re-embedding. This keeps QA latency low.

## Parameters

| Parameter | Value | Rationale |
|---|---|---|
| Window size | 400 words | Fits `nomic-embed-text`'s context window (~512 tokens) |
| Overlap | 80 words | 20 % overlap prevents splitting sentences at boundaries and preserves context |
| Chunker | `SimpleTextChunker` (word-boundary) | Swap for a token-aware or semantic chunker via `ITextChunker` without changing the pipeline |

## Trade-offs

| Pro | Con |
|---|---|
| Precise semantic retrieval on long documents | More rows in `DocumentChunks` (large files produce many chunks) |
| Context-window-safe for any model | Parameters require tuning when changing models |
| Exact snippet retrieval via char offsets | Must re-index all documents if chunk parameters change |
| No re-chunking overhead at query time | HNSW index build time grows with chunk count |

## Alternatives Considered

| Alternative | Rejected because |
|---|---|
| **Document-level embedding** | Context window truncation; loses precision on long docs |
| **Sentence-level chunking** | Too granular — loses contextual signal; ~10× more rows |
| **Semantic chunking** (topic boundary detection) | More accurate but significantly more complex; `ITextChunker` interface supports swapping this in later |
| **Azure AI Search / Elasticsearch** | Another external service; PostgreSQL + pgvector already handles this |
