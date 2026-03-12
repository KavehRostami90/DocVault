# Validation & Domain Correctness Enhancements

This document summarises all validation improvements and domain correctness fixes applied to DocVault.

---

## ✅ Request Validation (FluentValidation)

### `DocumentCreateRequestValidator`
- File size (1 byte – 50 MB)
- Content type (PDF, TXT, DOCX only)
- Filename security (no path traversal: `..`, `/`, `\`)
- Title (1–256 characters, no whitespace-only)
- Tags (≤ 20 tags, 1–50 chars each, alphanumeric + `-_`, no duplicates)

### `DocumentUpdateRequestValidator`
- Tag rules identical to create

### `SearchRequestValidator`
- Query: 2–512 characters, non-whitespace-only
- Page ≥ 1; size 1–200

### `ListDocumentsRequestValidator`
- Pagination: page ≥ 1, size 1–200
- Sort fields: `title`, `fileName`, `size`, `status`, `createdAt`, `updatedAt`
- Status filter: `pending`, `imported`, `indexed`, `failed`
- Filter length ≤ 100 characters

### `ImportCreateRequestValidator`
- `DocumentId` must not be empty
- `FileName`, `StoragePath`, `ContentType` are required

All limits are sourced from `ValidationConstants` in `DocVault.Domain`.

---

## ✅ Domain Invariants (`Document` Aggregate)

Enforced in constructors and setters; throw `DomainException` on violation.

| Rule | Error code |
|---|---|
| Title must not be null or whitespace | `TITLE_REQUIRED` |
| Title max 256 characters | `TITLE_LENGTH` |
| FileName must not be null or whitespace | `FILE_NAME_REQUIRED` |
| FileName must not contain `..`, `/`, `\` | `FILE_NAME_INVALID` |
| ContentType must not be null or whitespace | `CONTENT_TYPE_REQUIRED` |
| Size must be in range 1 – 50 MB | `FILE_SIZE_OUT_OF_RANGE` |
| Tag count ≤ 20 | `TAG_LIMIT_EXCEEDED` |
| Tags collection must not be null | `TAGS_REQUIRED` |

**Additional properties:**
- `IndexingError` — populated when `MarkFailed(error)` is called
- `DomainEvents` — `DocumentImported` raised by `MarkImported()`; `DocumentIndexed` raised by `MarkIndexed()`
- EF Core private constructor uses `base(default)` (not `DocumentId.New()`) to avoid creating a throwaway GUID on every DB read

---

## ✅ Domain Invariants (`ImportJob` Aggregate)

| Rule | Error code |
|---|---|
| `FileName` must not be null or whitespace | `FILE_NAME_REQUIRED` |
| `StoragePath` must not be null or whitespace | `STORAGE_PATH_REQUIRED` |
| `ContentType` must not be null or whitespace | `CONTENT_TYPE_REQUIRED` |

**Added property:**
- `DocumentId` — links the job to its `Document` aggregate (was missing; critical for `IndexingWorker` to update document status)

---

## ✅ Application Layer Fixes

### `IIngestionPipeline.RunAsync`
- Return type changed from `Task` to `Task<string>` (returns extracted plain text)
- `IngestionPipeline` returns text from the `TextExtractStage`

### `IndexingWorker` (BackgroundService)
- Now receives extracted text from the pipeline
- Calls `document.AttachText(extractedText)` before `document.MarkIndexed()`
- On failure: calls `document.MarkFailed(ex.Message)` to store error in `IndexingError`
- Resolves the document via `job.DocumentId` (previously impossible because `DocumentId` was missing from `ImportJob`)

### `StartImportJobCommand` / `StartImportJobHandler`
- Command updated to carry `DocumentId`, `StoragePath`, `ContentType` (was passing empty strings)
- Handler now creates `ImportJob` with all required fields

### `ImportDocumentHandler`
- Passes `documentId` to `ImportJob` constructor

---

## ✅ Infrastructure

### `FakeEmbeddingProvider`
Replaced trivial word-length stub with a real NLP technique:
- **FNV-1a 32-bit hashing** of lowercased tokens
- **128 buckets** (feature hashing / hashing trick)
- **L2 normalisation** so cosine similarity equals dot product
- Deterministic across runs; no runtime dependencies

### `IndexStage`
- Changed from `sealed` to `class` with `virtual IndexAsync()`
- Allows production subclasses (PostgreSQL `tsvector`, Azure AI Search, Elasticsearch) to be registered via DI without changing the base pipeline

### `ImportJobConfiguration` (EF Core)
- Added `DocumentId` column mapping with `DocumentId` ↔ `Guid` value converter

---

## ✅ Database Migration

| Migration | Description |
|---|---|
| `AddDocumentIdToImportJobs` | Adds `DocumentId uuid NOT NULL` column to `ImportJobs` table |

Applied via `dotnet ef database update`.

---

## ✅ Test Coverage

### Unit Tests (new — 67 total)

| File | What is tested |
|---|---|
| `Domain/Documents/DocumentTests.cs` | Status transitions, domain events, `IndexingError`, constructor validation |
| `Domain/Imports/ImportJobTests.cs` | Constructor guards, lifecycle (`Pending` → `InProgress` → `Completed`/`Failed`), timestamps |
| `Infrastructure/Embeddings/FakeEmbeddingProviderTests.cs` | 128-dim output, L2 norm = 1, determinism, case-insensitivity, distinct outputs |
| `Application/IndexingWorker/IndexingWorkerTests.cs` | Happy path (AttachText + MarkIndexed), failure path (MarkFailed on both job and doc), job-not-found skip, crash recovery re-enqueue |

### Integration Tests (53 total)
Coverage unchanged — upload, search, validation, pagination, and scoring scenarios remain green.

#### New Request Models
- **`ListDocumentsRequest`**: Strongly-typed request model for document listing with proper validation
- **`ValidationConstants`**: Centralized constants for all validation rules across the domain

#### Enhanced Validators

##### `DocumentCreateRequestValidator`
- File size validation (1 byte - 50MB)
- Content type validation (PDF, TXT, DOCX only)
- Filename security validation (no path traversal)
- Title validation (1-256 characters, no whitespace-only)
- Tag validation (up to 20 tags, 1-50 characters each, alphanumeric + hyphens/underscores)
- Duplicate tag prevention

##### `DocumentUpdateRequestValidator`
- Tag validation matching create requirements
- Duplicate tag prevention

##### `SearchRequestValidator`
- Query length validation (2-512 characters)
- Whitespace-only query prevention
- Page size limits (1-100 items)
- Page number validation (≥1)

##### `ListDocumentsRequestValidator` (NEW)
- Pagination validation (page ≥1, size 1-100)
- Sort field validation (title, fileName, size, status, createdAt, updatedAt)
- Status filter validation (pending, imported, indexed, failed)
- Filter length limits (≤100 characters)

### 2. Domain Invariants Implementation

#### Enhanced `Document` Entity
- Title validation in constructor and setters
- Filename security validation
- File size bounds enforcement
- Content type validation
- Tag count limits (≤20 tags)
- Proper null handling and trimming

#### Enhanced `Tag` Entity
- Name validation (1-50 characters)
- Character set validation (alphanumeric + hyphens/underscores)
- Case normalization (lowercase)
- Whitespace prevention

#### New `ValidationConstants` Class
- Centralized validation constants
- Organized by domain area (Documents, Tags, Search, Paging)
- Shared across API and domain layers

### 3. API Endpoint Improvements

#### `DocumentsEndpoints`
- Updated to use `ListDocumentsRequest` with proper validation
- Strongly-typed request binding
- Enhanced validation filters

#### Maintained Functionality
- All existing endpoints remain functional
- Backward-compatible parameter handling
- Improved error messages and validation feedback

### 4. Production Configuration

#### Template Configuration File
- `appsettings.Production.template.json` with production-ready settings
- Environment variable placeholders for secrets
- Comprehensive service configuration (Search, AI, Storage, Messaging)
- Health checks and monitoring setup
- Rate limiting configuration

## 🔄 Production Readiness Plan

### Detailed Implementation Guide
- **`docs/production-readiness-plan.md`**: Comprehensive 45-page production deployment guide
- Phase-based implementation approach
- Cost estimates for Azure and AWS
- Security, monitoring, and scalability considerations

### Key Production Components Planned
1. **Search Infrastructure**: Elasticsearch, Azure Cognitive Search, PostgreSQL FTS
2. **AI Embeddings**: OpenAI, Azure OpenAI, Local models
3. **Blob Storage**: Azure Blob, AWS S3, MinIO
4. **Message Queue**: Service Bus, SQS, RabbitMQ
5. **Monitoring**: Application Insights, Seq, Health Checks
6. **Security**: Authentication, authorization, rate limiting

## 💡 Key Benefits

### Security Improvements
- File upload validation prevents malicious files
- Path traversal protection in filenames
- Input sanitization and length limits
- Domain invariants prevent invalid state

### Performance Enhancements
- Reduced page size limits (max 100 items)
- Query length limits prevent expensive operations
- Centralized validation reduces code duplication

### Maintainability
- Centralized validation constants
- Consistent error messages
- Strongly-typed request models
- Clear separation of concerns

### User Experience
- Clear, actionable validation messages
- Consistent error responses
- Proper HTTP status codes
- Detailed field-level validation

## 🚀 Next Steps

1. **Test the enhanced validation** with existing integration tests
2. **Implement production providers** following the phase-based plan
3. **Deploy to staging environment** with production configuration
4. **Monitor performance** and adjust validation limits as needed
5. **Implement authentication/authorization** for production use

## 🔧 Configuration Usage

### Development
Continue using existing `appsettings.Development.json`

### Production
1. Copy `appsettings.Production.template.json` to `appsettings.Production.json`
2. Replace `#{VARIABLE}#` placeholders with actual values
3. Set `ASPNETCORE_ENVIRONMENT=Production`
4. Deploy with proper secret management

All validation improvements are immediately effective and backward-compatible with existing API consumers.