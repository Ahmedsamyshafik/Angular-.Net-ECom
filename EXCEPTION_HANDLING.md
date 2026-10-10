# Exception Handling & API Response Strategy

This document explains how the ECom API handles errors in a centralized, predictable
way, how the API contract looks for both success and failure, and where every piece
of the implementation lives.

---

## 1. Architecture & Request Flow

The project keeps its existing 3-Tier architecture:

| Layer | Project | Responsibility |
|---|---|---|
| Presentation (API) | `ECom.Api` | Controllers, HTTP status codes, ProblemDetails, exception→HTTP mapping |
| Business/Core | `Ecom.Core` | DTOs, entities, business exceptions, repository/service abstractions |
| Data Access | `ECom.Infrastructure` | `AppDbContext`, repositories, `UnitOfWork`, `ImageManagementService`, Hangfire |

### Failure flow

```
Request
  → UseExceptionHandler middleware (outermost)
  → Controller
      → Business logic runs (business decision logic lives in controllers in this project)
      → Load-operations call repository methods that enforce existence
        (GetByIdOrThrowAsync / DeleteOrThrowAsync) and throw NotFoundException
      → On other expected failures: throw BadRequestException / ConflictException
      → On unexpected failure: plain exception is thrown
  → GlobalExceptionHandler (IExceptionHandler)
      → identifies exception type
      → maps it to an HTTP status code and builds a ProblemDetails object
      → unexpected exceptions are logged (full details) but never returned to the client
  → ProblemDetails JSON response (application/problem+json)
  → Client
```

### Success flow

```
Request
  → Controller
  → Success: return Ok(new responseApi(200, message, data))
  → Client receives the standard API response
```

Key property: **errors never travel back through controllers**. Controllers only
return success (`responseApi`) or throw. All error shaping happens in one place.

---

## 2. Exception Types

All exceptions live in **`Src/Ecom.Core/Exceptions`** so they are available to
every layer and are free of HTTP concepts (no `IActionResult`, no status codes).

| Exception | HTTP status | When to throw | Status |
|---|---|---|---|
| `DomainException` | (never mapped directly) | Abstract base marking a meaningful application/business failure | Active |
| `NotFoundException` | 404 | A requested resource does not exist or has been soft-deleted. **Where thrown:** the repository layer via `GetByIdOrThrowAsync` / `DeleteOrThrowAsync` on `GenericRepo` (missing-id lookups), and the controllers for list-level checks (e.g. "No products found in this category") | Active (thrown now) |
| `BadRequestException` | 400 | Request violates a business rule (e.g. missing product ids / category id) | Active (thrown now) |
| `ConflictException` | 409 | A change conflicts with the current resource state | Available |
| _(not created)_ `UnauthorizedException` / `ForbiddenException` / `ValidationException` | 401 / 403 / 400 | — | **Deferred** — no auth and no validation framework exist yet. Auth + validation are a later phase. |

> **Why `DomainException` exists but is never mapped:** it is the common base class
> that documents *"this is a meaningful failure, not a bug"* and gives future layers
> (e.g. background jobs) one type to catch when they want to treat expected failures
> differently from bugs. The handler maps each derived exception explicitly and does
> **not** catch `DomainException` generically, so specific mappings always take
> precedence and the final `Exception` fallback remains a true 500.

> **No entity-specific exceptions** (e.g. `ProductNotFoundException`) were created:
> the base types are sufficient and keep the mapping table small and maintainable.

---

## 3. Exception → HTTP Status → ProblemDetails Mapping

Implemented in **`Src/ECom.Api/Exceptions/GlobalExceptionHandler.cs`** as a single
switch expression (easy to see and maintain):

| Exception | Status code | ProblemDetails title |
|---|---|---|
| `NotFoundException` | 404 | Not Found |
| `BadRequestException` | 400 | Bad Request |
| `ConflictException` | 409 | Conflict |
| `DbUpdateConcurrencyException` (EF Core) | 409 | Conflict |
| `BadHttpRequestException` (malformed body) | 400 | Bad Request |
| any other `Exception` | 500 | Internal Server Error |

Notes:
- `DbUpdateConcurrencyException` is matched **before** any general `DbUpdateException`
  (it derives from it). A general `DbUpdateException` (e.g. an SQL FK violation) is
  treated as an unexpected 500 — see "Remaining considerations".
- `BadHttpRequestException` is a real framework exception (invalid JSON, unreadable
  body) so mapping it to 400 is a known case, not speculation.

---

## 4. Standard API Response (`responseApi`)

`responseApi` is the pre-existing success envelope at
**`Src/ECom.Api/Helper/responseApi.cs`** and was **reused, not duplicated**:

```csharp
public class responseApi
{
    public int StatusCode { get; set; }        // e.g. 200
    public string? StatusMessage { get; set; } // human text
    public object? Data { get; set; }          // payload (DTO, list, ...)
}
```

**When it is returned:** only for **2xx** successes. Every successful endpoint
(`GetAll`, `GetById`, `Add`, `Update`, `Delete`, `ApplyDiscount`,
`ApplyCategoryDiscount`) returns `Ok(new responseApi(200, message, data))` unchanged.

Example:
```json
{
  "statusCode": 200,
  "statusMessage": "Products found.",
  "data": [ ... ]
}
```

---

## 5. ProblemDetails

**What it is:** the RFC 7807 standard error shape that ASP.NET Core knows natively.
Used for every **4xx / 5xx** response. It is produced by `GlobalExceptionHandler`
via `AddProblemDetails()` + auto-configured in `Program.cs`.

Example (404):
```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "Product not found.",
  "instance": "/api/product/GetById/999",
  "traceId": "0HLS0G8C..."
}
```

**Why ProblemDetails and not `responseApi` for errors?**
- It is the standardized, machine-readable error contract (widely supported by
  clients, tooling, and .NET itself).
- It keeps the **frontend contract predictable**: 2xx → `responseApi`, 4xx/5xx →
  ProblemDetails. Today errors were also wrapped in `responseApi`, making the two
  cases indistinguishable by shape.
- it never leaks internals (the handler controls exactly which fields go out).

`AddProblemDetails()` in `Program.cs` ensures framework-generated errors (e.g.
`[ApiController]` model-binding 400s) also follow the ProblemDetails shape.

---

## 6. Validation

There is **no validation framework and no data-annotation validation** in the
project today (no `[Required]`, no FluentValidation, no explicit ModelState code).
Per the phase scope, no validation was introduced.

What this means:
- No `ValidationException` exists (nothing triggers it yet).
- `[ApiController]` automatic model-binding failures (e.g. sending `"abc"` for an
  `int` id) already produce a 400 — and with `AddProblemDetails()` those are
  `ValidationProblemDetails`, which lists per-field errors and stays inside the
  ProblemDetails contract.
- Input validation (e.g. "Name is required", "Price > 0") is a **documented future
  concern** for the validation phase, not part of this task.

---

## 7. Unexpected Errors

- Any exception that is **not** one of the mapped types → **500** with a generic
  body: `"An unexpected error occurred. Please try again later."`
- The full exception (with stack trace, DB/SQL details) is written to **server logs**
  via `ILogger.LogError` including method and path for diagnostics.
- **Nothing internal is sent to the client:** no stack traces, no connection strings,
  no EF/SQL messages, no exception type names. The handler explicitly replaces
  framework messages for concurrency/malformed-body cases with safe wording.
- The unit test `UnexpectedException_ShouldReturn500GenericProblemDetailsAndLogError`
  asserts the response contains none of the original exception's sensitive text.

---

## 8. File Locations

| Concern | Path |
|---|---|
| Base exception | `Src/Ecom.Core/Exceptions/DomainException.cs` |
| `NotFoundException` | `Src/Ecom.Core/Exceptions/NotFoundException.cs` |
| `BadRequestException` | `Src/Ecom.Core/Exceptions/BadRequestException.cs` |
| `ConflictException` | `Src/Ecom.Core/Exceptions/ConflictException.cs` |
| Global handler (`IExceptionHandler`) | `Src/ECom.Api/Exceptions/GlobalExceptionHandler.cs` |
| Exception→HTTP mapping | inside `GlobalExceptionHandler.cs` (status switch) |
| ProblemDetails setup / registration | `Src/ECom.Api/Program.cs` (`AddProblemDetails`, `AddExceptionHandler`, `UseExceptionHandler`) |
| Standard success response model | `Src/ECom.Api/Helper/responseApi.cs` |
| **Missing-id enforcement (throws `NotFoundException`)** | `Src/Ecom.Core/Interfaces/IGenericRepo.cs` (contract: `GetByIdOrThrowAsync`, `DeleteOrThrowAsync`) + `Src/ECom.Infrastructure/Repositories/GenericRepo.cs` (implementation) |
| Controllers (throw for rule violations, no try/catch, no null-checks) | `Src/ECom.Api/Controllers/ProductController.cs`, `CategoryController.cs` |
| New handler tests | `Src/ECom.Api.Tests/GlobalExceptionHandlerTests.cs` |
| Modified controller tests | `Src/ECom.Api.Tests/ProductControllerTests.cs`, `CategoryControllerTests.cs` |
| Real-repo throw tests | `Src/ECom.Api.Tests/Repositories/RepositoryTests.cs` (InMemory DB) |
| Removed middleware | `Src/ECom.Api/Middleware/ExceptionHandlingMiddleware.cs` (deleted) |
| Removed old middleware tests | `Src/ECom.Api.Tests/ExceptionHandlingMiddlewareTests.cs` (deleted) |

---

## 9. API Response Contract (summary for the frontend)

| Case | HTTP status | Body shape |
|---|---|---|
| Success (read/update/delete/discount) | 200 | `responseApi { statusCode, statusMessage, data }` |
| Resource missing | 404 | `ProblemDetails` |
| Business rule violation / malformed input | 400 | `ProblemDetails` |
| Concurrency conflict | 409 | `ProblemDetails` |
| Unexpected failure | 500 | `ProblemDetails` (generic) |

Frontend rule of thumb: if the HTTP status starts with `2`, read `data` from the
`responseApi` body. Otherwise read the `ProblemDetails` body (`title`, `detail`,
`status`).

---

## 10. Learning Notes (what / why / where / alternatives)

### 10.1 Custom exceptions
1. **What:** `NotFound / BadRequest / Conflict` (plus a `DomainException` base).
2. **Why:** meaningful business failures need to be distinguishable from bug-level
   failures; throwing makes the flow explicit and keeps controllers readable.
3. **Why in `Ecom.Core`:** they represent business concerns and must be usable by any
   layer (API + future services) without dragging HTTP concepts into business code.
4. **Alternatives:** entity-specific exceptions (rejected – bloats the set with no
   benefit); returning `null`/result objects (rejected – pushes decision logic into
   every caller instead of one place).
5. **Why this approach:** smallest set that satisfies the mapping table; single
   location to document "what counts as a failure" in this business.

### 10.2 `IExceptionHandler` instead of middleware
1. **What:** replaced the hand-written `ExceptionHandlingMiddleware` with the
   framework's `IExceptionHandler` (net10-idiomatic).
2. **Why:** it is the modern, built-in mechanism for global exception→response work;
   no manual `RequestDelegate` plumbing, no duplicated response-writing logic.
3. **Why in `ECom.Api`:** HTTP concern → presentation layer.
4. **Alternatives:** keep extending the old middleware (rejected – duplicates what
   the framework now provides); `try/catch` in controllers (rejected – scattering).
5. **Why chosen:** .NET 10 supports it natively and it centralizes the mapping in one
   handler registered once.

### 10.3 The API layer throws intent; the repository enforces existence
1. **What:** business/rule failures are thrown from the controllers
   (`BadRequestException`, list-level `NotFoundException`), while **missing-id
   lookups are enforced by the repository layer**.
2. **Why:** the "does this record exist?" rule is a data-existence invariant that
   every caller needs; pushing it into `GenericRepo.GetByIdOrThrowAsync` /
   `DeleteOrThrowAsync` removes duplicated null-checks from the API layer and
   gives a single place where "not found" is decided.
3. **Where:** contract in `Ecom.Core` (`IGenericRepo`), throwing implementation in
   `ECom.Infrastructure` (`GenericRepo`) using Core's `NotFoundException` — the
   repository depends only on Core, not on HTTP.
4. **Alternatives:** keep `GetByIdAsync` returning `null` and let every controller
   null-check (rejected – repeated logic per endpoint); a service layer that runs
   this rule (out of scope – would restructure the project).
5. **Why this approach:** controllers keep business decision logic (they are the
   only place it runs today) but no longer repeat existence checks; the repository
   owns one consistent exception message (`"The requested {T.Name} was not found."`).
6. **Testing nuance:** controller tests that mock the repo now set up the throwing
   methods to simulate the missing case; the **real** throwing behavior is proven by
   repository tests against an InMemory `AppDbContext` (`RepositoryTests.cs`), not by
   the mocks.

### 10.4 ProblemDetails for errors, `responseApi` for success
1. **What:** kept `responseApi` for 2xx; all 4xx/5xx are now `ProblemDetails`.
2. **Why:** predictable, standard, documented contract for the frontend.
3. **Why located in `Program.cs` + handler:** registration belongs to composition
   root; body construction belongs to the handler.
4. **Alternatives:** one unified envelope for everything (rejected – had to inspect
   the status to know the shape and was non-standard); replace `responseApi` entirely
   (rejected – success contract works and was explicitly requested to stay).
5. **Why this split:** standardizes the failure path without touching the working
   success path.

### 10.5 Unexpected errors → 500 with logging only
1. **What:** generic body + full server-side logging.
2. **Why:** never leak internals; log enough (method, path, exception) to diagnose.
3. **Why in the handler:** it is the one place with both the logger and the response.
4. **Alternatives:** echo exception details in Development only (rejected for this
   phase – risky and unnecessary for the current consumers).
5. **Why chosen:** safe default; docs/tests pin the "no leak" behavior.

---

## 11. Remaining Considerations / Intentionally Not Changed

- **General `DbUpdateException` (e.g. SQL FK violation) → 500.** Adding a product with a
  non-existent `CategoryId` can surface as an FK error at `Commit`. It is recorded as 500
  (logged). A future refinement: validate the category exists (or map the FK violation to
  400/409) — requires care and is out of this task's scope.
- **No input validation was added** to any endpoint (per phase scope). Existing
  guard-style checks (empty product ids, missing category id) now throw
  `BadRequestException` instead of manually returning 400.
- **Optional lookups stay non-throwing.** In `ProductController.Add` / `Update` the
  category is loaded with `GetByIdAsync` purely for enrichment (`CategoryName`);
  a missing category there is not a failure of the requested resource, so the null
  check (not a `NotFoundException`) remains the right behavior.
- **401 / 403 / `UnauthorizedException` / `ForbiddenException`:** deferred entirely to
  the authentication/authorization phase.
- **Unknown-route 404** is still handled by ASP.NET routing (no explicit catch-all),
  and **Hangfire dashboard** behavior is unchanged.
- `responseApi.StatusCode` on success remains `200`; its shape is unchanged.

---

## 12. Cancellation Tokens

Every asynchronous I/O path (EF Core queries, `SaveChangesAsync`, file writes)
accepts a `CancellationToken`. This lets ASP.NET Core cancel the work when the
client aborts the request, instead of letting a query/file-write keep running.

Rules that are used consistently across the codebase:

1. **Controllers** declare `CancellationToken cancellationToken = default` per
   action — ASP.NET Core automatically binds it to `HttpContext.RequestAborted`
   when the request is cancelled. The default value exists so direct unit-test
   calls (`_controller.GetById(1)`) still compile.
2. **Layers** thread the token downward: controller → service/repo → EF Core
   (`ToListAsync(ct)`, `FindAsync(keys, ct)`, `AddAsync(ent, ct)`,
   `SaveChangesAsync(ct)`) and file I/O (`File.WriteAllBytesAsync(path, data, ct)`).
3. **Signature convention:** `CancellationToken` is the **last parameter** with
   `default` when there are no `params`. For methods that end in a `params` array
   (e.g. `GetByIdOrThrowAsync(ct, keyValues, includes...)`),
   the token is placed **before** the `params` array so `params` stays last.
   The token-first overload is strictly more specific than the raw `params object[]`
   overload, so calls like `GetByIdOrThrowAsync(ct, 5)` bind unambiguously.
4. **Not tokenized (intentionally):** `HardDeleteImage` (pure in-memory/sync,
   no I/O) and the Hangfire background jobs (`PurgeImageFileAsync`,
   `PurgeOldDeletedImagesAsync`) — they have no client request to watch; they keep
   their existing signatures.
5. **Mocks/tests:** Moq treats an *omitted* optional parameter as "match any",
   so setups like `Setup(r => r.AddAsync(It.IsAny<TblProducts>()))` still match
   calls carrying a token — except when `ReturnsAsync((TblProducts p) => p)`
   lambdas are used, because Moq then types the callback to the full argument list
   (use `(TblProducts p, CancellationToken ct) => p`).

---

## 13. Implementation Flow Study Guide

1. Read `DomainException.cs` → the abstract base; then `NotFound/BadRequest/Conflict`
   → the three business failures currently used.
2. Read `IGenericRepo.cs` → the existence-enforcing repository contract
   (`GetByIdOrThrowAsync` ×2, `DeleteOrThrowAsync`); then `GenericRepo.cs` →
   implementations that call the nullable `GetByIdAsync`/`DeleteAsync` and throw
   `NotFoundException` when the record is missing/soft-deleted.
3. Read `GlobalExceptionHandler.cs` → the single switch mapping exception type to
   status; the logger calls; the `detail` switch (safe vs. verbatim); the manual
   `ProblemDetails` construction with `traceId`; the raw `WriteAsync` that keeps the
   proper `application/problem+json` content type.
4. Read `Program.cs` → `AddProblemDetails` + `AddExceptionHandler<GlobalExceptionHandler>`
   (services) and `UseExceptionHandler()` (pipeline, outermost).
5. Read the controllers → missing-id loads use `GetByIdOrThrowAsync` /
   `DeleteOrThrowAsync` (no null-checks left in the API layer); rule violations throw
   `BadRequestException` / list-level `NotFoundException`; every success branch returns
   `Ok(new responseApi(...))`; there are no try/catch blocks.
6. Read `responseApi.cs` → the unchanged 2xx envelope.
7. Read the tests:
   - `ProductControllerTests.cs` / `CategoryControllerTests.cs` → success paths still
     assert `responseApi`; missing-record mocks are set up to **throw** `NotFoundException`
     (simulating the real repo); malformed-input tests assert `BadRequestException`;
     concurrency tests assert the `DbUpdateConcurrencyException` simply propagates.
   - `Repositories/RepositoryTests.cs` → proves the **real** `GenericRepo` throws
     `NotFoundException` for missing ids and soft-deletes found records (InMemory DB).
   - `GlobalExceptionHandlerTests.cs` → proves each exception type → correct status,
     ProblemDetails fields, safe message for framework exceptions, no-leak on 500,
     and that unexpected exceptions are logged at `Error`.

Flow to trace end-to-end: `GET /api/product/GetById/999` → controller →
`GetByIdOrThrowAsync` → real repo returns nothing → `throw NotFoundException` →
`UseExceptionHandler` runs `GlobalExceptionHandler` → 404 `ProblemDetails` → logged
warning → client.