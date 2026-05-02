# ADR-004 — Result\<T\> for Expected Failures, Not Exceptions

**Status:** Accepted

---

## Context

Application handlers need to communicate two kinds of outcomes to the API layer:

- **Expected failures** — document not found, duplicate hash, validation violation, unauthorized access
- **Unexpected failures** — database connection lost, storage write failed, bug in code

Two common approaches exist:

1. **Throw exceptions** for all failures and catch them in middleware
2. **Return a discriminated union** (`Result<T>`) for expected failures; let unexpected ones propagate

## Decision

Use `Result<T>` for all expected failure paths in handlers. Unexpected exceptions propagate naturally and are caught by `GlobalExceptionHandler`, which translates them to RFC 7807 `ProblemDetails`.

```csharp
// Handler returns a Result, never throws for expected failures
public async Task<Result<DocumentId>> HandleAsync(ImportDocumentCommand command, ...)
{
    if (duplicate) return Result<DocumentId>.Failure(Errors.DuplicateDocument);
    // ...
    return Result<DocumentId>.Success(documentId);
}

// Endpoint maps Result to HTTP status
var result = await handler.HandleAsync(command, ct);
return result.IsSuccess
    ? Results.Ok(result.Value)
    : Results.Problem(result.Error);
```

## Reasoning

### 1 — Exceptions are for exceptional things

Exceptions carry a stack trace and interrupt normal control flow. Using them for "document not found" — a completely expected condition — makes code harder to read and logs noisy.

### 2 — Explicit, compiler-visible failure paths

`Result<T>` forces the caller to handle the failure case at the call site. Exception-based code has no such guarantee — a missing `catch` block compiles silently.

### 3 — Performance

Exception construction is expensive (stack trace capture). On hot paths (search, list) this matters. `Result<T>` is a plain struct — zero allocation overhead.

### 4 — Testability

Unit tests assert `result.IsSuccess` and `result.Error.Code` without `Assert.Throws`. The expected-failure path is just another code path, not a special testing mode.

## Trade-offs

| Pro | Con |
|---|---|
| Explicit, compiler-enforced error handling | More verbose than `throw` |
| No stack trace overhead for expected failures | Callers must always check `IsSuccess` |
| Clean unit test assertions | Two error-handling mechanisms to understand |
| Unexpected failures still propagate with stack trace | Requires discipline not to ignore the failure case |

## What Still Uses Exceptions

- **Domain invariant violations** (`DomainException`) — thrown inside aggregates, caught by `GlobalExceptionHandler`
- **Infrastructure failures** — DB connection loss, storage write failure — unexpected, always exceptions
- **Bugs** — `NullReferenceException`, etc. — always exceptions
