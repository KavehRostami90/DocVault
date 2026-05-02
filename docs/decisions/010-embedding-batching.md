# ADR-010 — Embedding Batching (O(n) → O(1) HTTP Round-Trips)

**Status:** Accepted

---

## Context

During document indexing, each document is split into multiple text chunks (400-word windows, 80-word overlap). A 10-page document typically produces 15–30 chunks; a 100-page PDF can produce 200–400 chunks. Each chunk needs a vector embedding.

Two approaches were considered:

1. **Sequential embedding** — call `EmbedAsync(text)` once per chunk in a loop
2. **Batch embedding** — call `EmbedBatchAsync(texts[])` once per document with all chunks in a single request

## Decision

Use **batch embedding** via `IEmbeddingProvider.EmbedBatchAsync`. The ingestion pipeline calls `GenerateBatchAsync` once per document regardless of chunk count. `OpenAiEmbeddingProvider` implements native batching — all chunk texts are sent in a single HTTP POST to the `/embeddings` endpoint.

## Implementation

### Interface (default sequential fallback)

```csharp
// IEmbeddingProvider — default implementation calls EmbedAsync sequentially.
// Providers that support native batching override this.
async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken = default)
{
    var results = new float[texts.Count][];
    for (var i = 0; i < texts.Count; i++)
        results[i] = await EmbedAsync(texts[i], cancellationToken);
    return results;
}
```

### OpenAiEmbeddingProvider — native batch (O(1) round-trips)

```csharp
// Sends all chunk texts in one POST /embeddings request.
// Splits into sub-batches of MaxBatchSize when chunk count exceeds the API limit.
public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
    IReadOnlyList<string> texts, CancellationToken ct)
{
    for (var offset = 0; offset < texts.Count; offset += maxBatchSize)
    {
        var batch   = texts.GetRange(offset, batchSize);
        var vectors = await SendBatchAsync(batch, ct);  // one HTTP POST per sub-batch
        allVectors.AddRange(vectors);
    }
    return allVectors;
}
```

### Pipeline usage

```csharp
// IngestionPipeline.RunAsync — one call regardless of chunk count
var texts   = textChunks.Select(c => c.Text).ToList();
var vectors = await _embedding.GenerateBatchAsync(texts, cancellationToken);
```

## Performance Impact

| Approach | HTTP calls for 30 chunks | Latency (approx.) |
|---|---|---|
| Sequential `EmbedAsync` per chunk | 30 | 30 × 200 ms = **6 s** |
| Batch `EmbedBatchAsync` (MaxBatchSize=96) | 1 | **~300 ms** |
| Batch with 200 chunks (MaxBatchSize=96) | 3 sub-batches | **~900 ms** |

For a typical document, batching reduces embedding time by **~20×**. For a large PDF, the saving is even greater.

### Why network latency dominates

Each HTTP call to an embedding API incurs:
- TCP connection setup (or HTTP/2 stream overhead)
- TLS handshake (first call)
- Server-side model loading latency
- JSON serialisation / deserialisation round-trip

These fixed-cost overheads make sequential calls expensive regardless of text size. Batching amortises them across all chunks.

## MaxBatchSize configuration

`OpenAiOptions.MaxBatchSize` (default: 96) controls how many chunk texts are sent per HTTP request. OpenAI's API accepts up to 2048 inputs per call; Ollama's limit varies by model. The provider splits large batches into sub-batches automatically:

```json
{
  "OpenAI": {
    "MaxBatchSize": 96
  }
}
```

## Ordering guarantee

The OpenAI `/embeddings` response includes an `index` field per result. `OpenAiEmbeddingProvider` sorts by `index` before returning, preserving the exact input order regardless of how the API returns results. This is critical — `IngestionPipeline` zips chunks with vectors by position:

```csharp
var chunkEmbeddings = textChunks
    .Zip(vectors, (chunk, vector) => new ChunkEmbedding(chunk, vector))
    .ToList();
```

## Trade-offs

| Pro | Con |
|---|---|
| ~20× latency reduction for typical documents | Larger request body (all chunk texts in one POST) |
| Amortises HTTP/TLS overhead across all chunks | If the API rejects the batch, all chunks fail together |
| MaxBatchSize cap handles API input limits | MaxBatchSize must be tuned per provider |
| Default interface implementation is sequential — any provider works | Providers that don't override `EmbedBatchAsync` get sequential behaviour silently |

## Provider Compatibility

| Provider | Batching | Notes |
|---|---|---|
| `OpenAiEmbeddingProvider` | ✅ Native | Sends all chunks in one request; splits at `MaxBatchSize` |
| `FakeEmbeddingProvider` | ✅ Via default | Uses sequential fallback in the interface default method |
| Any future provider | ✅ Via default | Inherits sequential fallback automatically; override for native batching |
