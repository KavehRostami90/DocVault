# DocVault Production Readiness Plan

## Overview
This document tracks the production readiness of DocVault, recording what is already implemented, what is a development stub, and the recommended path to a production-grade deployment.

---

## Current State

### ✅ Production-Ready Components

- **Clean Architecture** — strict layer separation, dependency inversion throughout
- **Domain invariants** — all business rules enforced in `Document` and `ImportJob` aggregates; `DomainException` on violation
- **Input validation** — FluentValidation with domain-level `ValidationConstants`; covers file size, content type, path traversal, tag limits, query length
- **Error handling** — `GlobalExceptionHandler` translates exceptions to RFC 7807 `ProblemDetails`
- **Structured logging** — Serilog 9 with source-generated `[LoggerMessage]` throughout; JSON output
- **Correlation IDs** — `CorrelationIdMiddleware` injects `X-Correlation-Id` on every request
- **Crash recovery** — `IndexingWorker` re-enqueues `Pending`/`InProgress` jobs on startup
- **Domain events** — `DocumentImported` and `DocumentIndexed` raised and dispatched in-process
- **EF Core migrations** — all schema changes tracked and applied via `dotnet ef database update`
- **Integration tests** — 53 tests covering upload, search, validation, pagination, and scoring
- **Unit tests** — 67 tests covering domain aggregates, handlers, embedding provider, and worker

### ⚠️ Development Stubs (swap before going live)

| Component | Current implementation | Recommended production replacement |
|---|---|---|
| Embeddings | `FakeEmbeddingProvider` — FNV-1a feature hashing, 128-dim L2-normalised | OpenAI `text-embedding-3-large`, Azure OpenAI, or local ONNX model |
| File storage | `LocalFileStorage` — writes `{id}.bin` to `/app/storage` | Azure Blob Storage, AWS S3, or MinIO |
| Work queue | `ChannelWorkQueue<T>` — in-memory, lost on restart | `PostgresWorkQueue` (already implemented) — enables multi-instance deployments |
| Search index | `IndexStage` — virtual no-op base class | Subclass with PostgreSQL `tsvector`/`tsquery`, Azure AI Search, or Elasticsearch |

---

## Implementation Phases

### Phase 1 — Core Production Infrastructure (Critical)

#### 1.1 Persistent Work Queue
Swap `ChannelWorkQueue<T>` for the already-implemented `PostgresWorkQueue`:

```csharp
// src/DocVault.Infrastructure/DependencyInjection.cs
// Change:
services.AddSingleton<IWorkQueue<IndexingWorkItem>, ChannelWorkQueue<IndexingWorkItem>>();
// To:
services.AddSingleton<IWorkQueue<IndexingWorkItem>, PostgresWorkQueue>();
```

#### 1.2 Production File Storage
Implement `AzureBlobFileStorage : IFileStorage`:

```csharp
// src/DocVault.Infrastructure/Storage/AzureBlobFileStorage.cs
public sealed class AzureBlobFileStorage : IFileStorage
{
    // BlobServiceClient injected via DI
    // WriteAsync  → BlobClient.UploadAsync
    // ReadAsync   → BlobClient.DownloadStreamingAsync
    // DeleteAsync → BlobClient.DeleteIfExistsAsync
}
```

Configuration:
```json
{
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "#{AZURE_STORAGE_CONNECTION_STRING}#",
      "ContainerName": "docvault-documents"
    }
  }
}
```

#### 1.3 Database Connection Pooling
Add PgBouncer or enable Npgsql connection multiplexing:

```json
{
  "ConnectionStrings": {
    "Default": "Host=…;Port=5432;Database=docvault;Username=docvault;Password=…;Pooling=true;MinPoolSize=5;MaxPoolSize=100;Multiplexing=true"
  }
}
```

#### 1.4 Health Checks
```csharp
services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddCheck<FileStorageHealthCheck>("storage");
```

---

### Phase 2 — Full-Text Search

Subclass `IndexStage` to write to a real search index. Example using PostgreSQL `tsvector`:

```csharp
// src/DocVault.Infrastructure/Search/PostgresFullTextIndexStage.cs
public sealed class PostgresFullTextIndexStage : IndexStage
{
    public override async Task IndexAsync(string text, float[] vector, CancellationToken ct)
    {
        // UPDATE Documents SET SearchVector = to_tsvector('english', @text) WHERE Id = @id
    }
}
```

Update `EfDocumentRepository.SearchAsync` to use `tsvector` queries instead of `LIKE`.

---

### Phase 3 — Real Embeddings

Implement `OpenAIEmbeddingProvider : IEmbeddingProvider`:

```csharp
// src/DocVault.Infrastructure/Embeddings/OpenAIEmbeddingProvider.cs
public sealed class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    // POST https://api.openai.com/v1/embeddings
    // Model: text-embedding-3-large (3072-dim) or text-embedding-ada-002 (1536-dim)
}
```

Configuration:
```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "#{OPENAI_API_KEY}#",
      "Model": "text-embedding-3-large"
    }
  }
}
```

> **Note:** The embedding dimension in `IndexingQueueEntries` and any vector column must be updated to match the chosen model.

---

### Phase 4 — Security & Scalability

#### Authentication
```csharp
services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Auth:Authority"];
        options.Audience  = configuration["Auth:Audience"];
    });
```

#### Rate Limiting
```csharp
services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("upload", o =>
    {
        o.PermitLimit = 10;
        o.Window      = TimeSpan.FromMinutes(1);
    }));
```

#### Secrets Management
Use Azure Key Vault or AWS Secrets Manager; never commit credentials.

```bash
export DOCVAULT_DB="Host=…"
export DOCVAULT_STORAGE_KEY="…"
export DOCVAULT_AI_KEY="…"
```

---

## Deployment Checklist

- [ ] Switch `IWorkQueue<T>` to `PostgresWorkQueue`
- [ ] Implement and register production `IFileStorage`
- [ ] Implement and register production `IEmbeddingProvider`
- [ ] Subclass `IndexStage` with real index writes
- [ ] Update `SearchAsync` in `EfDocumentRepository` to use full-text search
- [ ] Configure database connection string via environment variable
- [ ] Apply all EF migrations to production database
- [ ] Add health checks for DB and storage
- [ ] Configure TLS / reverse proxy
- [ ] Set up structured log sink (Seq, Application Insights, or Datadog)
- [ ] Set up alerting on `DocumentStatus.Failed` spike
- [ ] Implement authentication/authorisation
- [ ] Add rate limiting on upload endpoints
- [ ] Run `dotnet publish -c Release` and verify Docker image builds cleanly
- [ ] Load test the ingestion pipeline under concurrent uploads

---

## Cost Estimates (Medium Deployment)

### Azure
| Service | Est. monthly |
|---|---|
| App Service (P2v3) | $150–250 |
| Azure Database for PostgreSQL Flexible | $150–400 |
| Blob Storage (Hot tier, 100 GB) | $5–20 |
| Azure AI Search (Basic) | $75–250 |
| OpenAI Embeddings API | variable (per token) |
| **Total** | **~$400–950 / month** |

### AWS
| Service | Est. monthly |
|---|---|
| ECS Fargate | $100–200 |
| RDS PostgreSQL (db.t3.medium) | $100–300 |
| S3 (Standard, 100 GB) | $3–10 |
| OpenSearch Service (t3.small) | $50–150 |
| OpenAI Embeddings API | variable (per token) |
| **Total** | **~$300–700 / month** |

## 1. Search Infrastructure

### Current State (Development)
```csharp
// src/DocVault.Infrastructure/Search/FakeSearchService.cs
// Uses in-memory collections with basic text matching
```

### Production Requirements
- **Elasticsearch/OpenSearch** for full-text search with vector embeddings
- **Azure Cognitive Search** for cloud-native solution
- **PostgreSQL Full-Text Search** for simpler deployment

### Implementation Plan
```csharp
// Interface (already exists)
public interface ISearchService
{
    Task IndexDocumentAsync(Document document, CancellationToken cancellationToken = default);
    Task<SearchResult> SearchAsync(string query, int page, int size, CancellationToken cancellationToken = default);
}

// Production implementations needed:
// src/DocVault.Infrastructure/Search/ElasticsearchSearchService.cs
// src/DocVault.Infrastructure/Search/AzureCognitiveSearchService.cs
// src/DocVault.Infrastructure/Search/PostgreSqlSearchService.cs
```

### Configuration
```json
{
  "Search": {
    "Provider": "Elasticsearch", // or "AzureCognitive", "PostgreSql"
    "Elasticsearch": {
      "Uri": "https://elasticsearch.company.com:9200",
      "IndexName": "docvault-documents",
      "Username": "",
      "Password": ""
    },
    "AzureCognitive": {
      "ServiceName": "docvault-search",
      "AdminKey": "",
      "QueryKey": "",
      "IndexName": "documents"
    }
  }
}
```

## 2. Embeddings Infrastructure

### Current State (Development)
```csharp
// src/DocVault.Infrastructure/AI/FakeEmbeddingService.cs
// Returns random vectors
```

### Production Requirements
- **OpenAI Embeddings API** for text-embedding-ada-002 or text-embedding-3-large
- **Azure OpenAI** for enterprise compliance
- **Local models** via Ollama/LlamaIndex for air-gapped environments

### Implementation Plan
```csharp
// Interface (already exists)
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

// Production implementations needed:
// src/DocVault.Infrastructure/AI/OpenAIEmbeddingService.cs
// src/DocVault.Infrastructure/AI/AzureOpenAIEmbeddingService.cs
// src/DocVault.Infrastructure/AI/LocalEmbeddingService.cs
```

### Configuration
```json
{
  "AI": {
    "Provider": "OpenAI", // or "AzureOpenAI", "Local"
    "OpenAI": {
      "ApiKey": "",
      "Model": "text-embedding-3-large",
      "BaseUrl": "https://api.openai.com/v1"
    },
    "AzureOpenAI": {
      "Endpoint": "https://company.openai.azure.com/",
      "ApiKey": "",
      "DeploymentName": "text-embedding-ada-002"
    }
  }
}
```

## 3. Blob Storage Infrastructure

### Current State (Development)
```csharp
// src/DocVault.Infrastructure/Storage/LocalFileStorage.cs
// Stores files in local temp directory
```

### Production Requirements
- **Azure Blob Storage** for cloud deployment
- **AWS S3** for AWS environments
- **MinIO** for on-premises S3-compatible storage
- **Network File System (NFS)** for traditional deployments

### Implementation Plan
```csharp
// Interface (already exists)
public interface IBlobStorage
{
    Task<string> StoreAsync(string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> RetrieveAsync(string blobId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobId, CancellationToken cancellationToken = default);
}

// Production implementations needed:
// src/DocVault.Infrastructure/Storage/AzureBlobStorage.cs
// src/DocVault.Infrastructure/Storage/S3BlobStorage.cs
// src/DocVault.Infrastructure/Storage/MinIOBlobStorage.cs
```

### Configuration
```json
{
  "Storage": {
    "Provider": "AzureBlob", // or "S3", "MinIO", "Local"
    "AzureBlob": {
      "ConnectionString": "",
      "ContainerName": "docvault-documents"
    },
    "S3": {
      "BucketName": "docvault-documents",
      "Region": "us-east-1",
      "AccessKey": "",
      "SecretKey": ""
    }
  }
}
```

## 4. Message Queue Infrastructure

### Current State (Development)
```csharp
// src/DocVault.Infrastructure/Messaging/InMemoryQueue.cs
// Uses in-memory collections
```

### Production Requirements
- **Azure Service Bus** for cloud-native messaging
- **Amazon SQS** for AWS environments
- **RabbitMQ** for on-premises deployment
- **PostgreSQL** as a message queue for simple deployments

### Implementation Plan
```csharp
// Interface (already exists)
public interface IMessageQueue
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
    Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken = default) where T : class;
}

// Production implementations needed:
// src/DocVault.Infrastructure/Messaging/ServiceBusQueue.cs
// src/DocVault.Infrastructure/Messaging/SqsQueue.cs
// src/DocVault.Infrastructure/Messaging/RabbitMqQueue.cs
// src/DocVault.Infrastructure/Messaging/PostgreSqlQueue.cs
```

## 5. Database Migrations & Deployment

### Current State
- Entity Framework Core with in-memory database for testing
- PostgreSQL configured but needs production setup

### Production Requirements
- **Database Migration Strategy**: Blue-green, rolling, or maintenance windows
- **Connection Pooling**: PgBouncer or built-in pooling
- **High Availability**: Primary/replica setup with read replicas
- **Backup Strategy**: Point-in-time recovery, daily backups

### Migration Scripts
```bash
# Production deployment script
#!/bin/bash
dotnet ef database update --connection "$PROD_CONNECTION_STRING"
dotnet run --environment Production
```

## 6. Configuration & Secrets Management

### Production Requirements
```csharp
// src/DocVault.Infrastructure/Configuration/ProductionConfigurationExtensions.cs
public static class ProductionConfigurationExtensions
{
    public static void AddProductionConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Azure Key Vault integration
        services.AddAzureKeyVault(configuration);
        
        // AWS Secrets Manager integration
        services.AddAwsSecretsManager(configuration);
        
        // Environment variable configuration
        services.Configure<SearchOptions>(configuration.GetSection("Search"));
        services.Configure<AIOptions>(configuration.GetSection("AI"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
    }
}
```

## 7. Monitoring & Observability

### Production Requirements
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.ApplicationInsights", "Serilog.Sinks.Seq"],
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { 
        "Name": "ApplicationInsights",
        "Args": { "instrumentationKey": "#{AI_INSTRUMENTATION_KEY}#" }
      },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "#{SEQ_URL}#" }
      }
    ]
  },
  "ApplicationInsights": {
    "InstrumentationKey": "#{AI_INSTRUMENTATION_KEY}#"
  }
}
```

## 8. Health Checks & Readiness Probes

### Implementation
```csharp
// src/DocVault.Api/Health/DocVaultHealthChecks.cs
public static class DocVaultHealthChecks
{
    public static IServiceCollection AddDocVaultHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Default")!)
            .AddCheck<SearchServiceHealthCheck>("search")
            .AddCheck<BlobStorageHealthCheck>("storage")
            .AddCheck<MessageQueueHealthCheck>("messaging")
            .AddCheck<EmbeddingServiceHealthCheck>("ai");
        
        return services;
    }
}
```

## 9. Performance & Scalability

### Caching Strategy
```csharp
// Redis for distributed caching
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});

// Memory caching for single instance
services.AddMemoryCache();
```

### Rate Limiting
```csharp
// src/DocVault.Api/Middleware/RateLimitingMiddleware.cs
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("DocumentUpload", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});
```

## 10. Security Considerations

### Authentication & Authorization
```csharp
services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Auth:Authority"];
        options.Audience = configuration["Auth:Audience"];
    });

services.AddAuthorization(options =>
{
    options.AddPolicy("DocumentUpload", policy =>
        policy.RequireClaim("scope", "documents:write"));
});
```

## Implementation Priority

### Phase 1 (Critical for Production)
1. ✅ Enhanced validation and domain invariants (COMPLETED)
2. Database migration strategy
3. Production blob storage (Azure/S3)
4. Health checks and monitoring

### Phase 2 (Enhanced Features)
1. Production search infrastructure (Elasticsearch)
2. Real embeddings service (OpenAI/Azure)
3. Message queue (Service Bus/SQS)
4. Caching and rate limiting

### Phase 3 (Scale & Optimize)
1. Performance monitoring and optimization
2. Horizontal scaling capabilities
3. Advanced security features
4. Disaster recovery planning

## Deployment Checklist

- [ ] Environment-specific configuration files
- [ ] Database connection strings and credentials
- [ ] SSL/TLS certificates
- [ ] Load balancer configuration
- [ ] Container orchestration (if using Docker/Kubernetes)
- [ ] Monitoring and alerting setup
- [ ] Backup and disaster recovery procedures
- [ ] Security scanning and vulnerability assessment

## Cost Considerations

### Azure Estimate (Medium deployment)
- App Service: $100-200/month
- Azure SQL Database: $200-500/month
- Blob Storage: $20-50/month
- Cognitive Search: $250-500/month
- Service Bus: $10-30/month
- **Total: ~$600-1300/month**

### AWS Estimate (Medium deployment)
- EC2/ECS: $100-200/month
- RDS PostgreSQL: $150-400/month
- S3: $20-50/month
- OpenSearch: $200-400/month
- SQS: $5-15/month
- **Total: ~$500-1100/month**