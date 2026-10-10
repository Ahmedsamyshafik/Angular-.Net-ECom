# Rate Limiting — Implementation (built-in ASP.NET Core, net10.0)

> Status: **implemented**. This document describes the final production architecture:
> built-in ASP.NET Core Rate Limiting only. There is **no** custom middleware and no
> hand-written algorithm.

---

## 1. Goal

Protect the public REST endpoints from abusive request rates (brute-force,
scraping, accidental client loops) while keeping the existing contract unchanged:

- success → `responseApi { StatusCode, StatusMessage, Data }` (unchanged)
- failure → `ProblemDetails` (unchanged; 429 stays inside the same contract)

Rate limiting is an **edge protection** concern: it must run before controllers and
should consume as little server work as possible. This is why it is implemented with
ASP.NET Core's built-in middleware (`AddRateLimiter` + `UseRateLimiter`) rather than
hand-written code.

Limits come from `appsettings.json` (`RateLimiting` section), so behavior can be
tuned without recompiling.

---

## 2. Production Architecture

Two independent, complementary layers. A request must pass **both** to continue.

| Layer | Limiter | Algorithm | Scope |
|-------|---------|-----------|-------|
| 1 — Global | `GlobalLimiter` | **Sliding Window** | Server-wide, single partition |
| 2 — Client | `per-client` policy | **Token Bucket** | Partitioned per client |

### Layer 1 — Global sliding window (server-wide)

`RateLimiterOptions.GlobalLimiter` is a `PartitionedRateLimiter` whose partition
factory always returns the **constant** key `global` (`RateLimitingRegisteration.GlobalPartitionKey`).
Because the partition key never varies, every request — authenticated or not, from
any IP — draws from **one** shared sliding window. This is what makes it truly
global: it caps the total throughput the application can handle.

```
Ahmed    ─┐
Mohamed  ─┤──→ Global Sliding Window ("global" partition)
Ali      ─┤
Anonymous ─┘
```

### Layer 2 — Per-client token bucket

A named endpoint policy `per-client` (built with `RateLimitPartition.GetTokenBucketLimiter`)
applies an independent bucket to each client. Controllers opt into it with a
class-level `[EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]`.
The bucket replenishes via `TokensPerPeriod` every `ReplenishmentPeriodSeconds`.

```
Authenticated → UserId → Own token bucket
Anonymous     → IP     → Own token bucket
```

There is deliberately **no third "stricter write endpoint" limiter** — the design
is exactly these two layers.

---

## 3. Partitioning

The per-client key is computed by `RateLimitingRegisteration.GetClientPartitionKey(HttpContext)`:

```csharp
if (context.User.Identity?.IsAuthenticated == true)
    key = $"user:{context.User.FindFirstValue(ClaimTypes.NameIdentifier)}";
else
    key = $"ip:{context.Connection.RemoteIpAddress}"   // ?? "ip:unknown"
```

| Client | Partition key | Notes |
|--------|---------------|-------|
| Authenticated | `user:{UserId}` | Uses `ClaimTypes.NameIdentifier`. IP is **not** used for authenticated requests. |
| Authenticated but claim missing | `ip:{RemoteIpAddress}` | Defensive fallback keeps the request limited. |
| Anonymous | `ip:{RemoteIpAddress}` | Falls back to `ip:unknown` if the peer address is unavailable. |

> **Current authentication state in this project:** `Program.cs` does not yet
> configure any authentication (`AddAuthentication`/`AddJwtBearer`) and controllers
> have no `[Authorize]`. So at runtime every request is anonymous today and lands on
> `ip:{RemoteIpAddress}`. The UserId branch is implemented and tested so it works the
> moment an authentication scheme is added — `ClaimTypes.NameIdentifier` is the
> conventional subject/name-identifier claim and can be pointed at the real claim then.
> Do **not** assume a different claim without checking the future auth setup.

### Forwarded headers

`Program.cs` does **not** call `UseForwardedHeaders`, and no trusted proxy is
configured. The application therefore uses the real socket peer (`Connection.RemoteIpAddress`),
which is correct for direct/localhost usage. `X-Forwarded-For` is **not** trusted:
behind Kestrel the header is arbitrary client input. When the app is later deployed
behind a reverse proxy, enable `UseForwardedHeaders` with `KnownProxies`/`KnownNetworks`
**before** `UseRateLimiter`, then re-test partitioning. See §10.

---

## 4. Request Flow

```
                        HTTP Request
                             │
                             ▼
                     ┌──────────────────────┐
                     │  Global Limiter       │  Layer 1
                     │  Sliding window       │  single "global" partition
                     │  server-wide          │
                     └───────────┬──────────┘
                                 │
                          GLOBAL ALLOWED?
                           /          \
                        NO             YES
                         │              │
                         ▼              ▼
                      429   ┌──────────────────────┐
                      │     │  Client Limiter       │  Layer 2
                      │     │  Token bucket         │  partitioned match
                      │     │  UserId / IP          │
                      │     └───────────┬──────────┘
                      │                 │
                      │        CLIENT ALLOWED?
                      │           /       \
                      │        NO           YES
                      │        │             │
                      │        ▼             ▼
                      ├──► 429     Example: Access-Control-Allow-Origin
                      |          Authentication / Authorization
                      |                 (none configured today)
                      |                    │
                      |                    ▼
                      |                Controller
                      |                    │
                      |              Authorization
                      |                (none)
                      |                    ▼
                      |              Endpoint executes
```

Both 429 paths (global exhausted, or client bucket exhausted) are produced by the
**same** limiter rejection callback, so the response shape is identical.

---

## 5. Configuration

`appsettings.json` → `RateLimiting`:

```jsonc
"RateLimiting": {
  "Global": {
    "PermitLimit": 1000,          // max requests allowed per window (server-wide)
    "WindowSeconds": 60,          // length of the sliding window
    "SegmentsPerWindow": 4,       // sliding-window smoothing: how finely the window is split
    "QueueLimit": 0               // queued requests when full (0 = reject immediately)
  },
  "Client": {
    "TokenLimit": 30,             // burst capacity of each client bucket
    "TokensPerPeriod": 30,        // tokens refilled per replenishment period
    "ReplenishmentPeriodSeconds": 60, // how often the bucket refills
    "QueueLimit": 0               // 0 = reject immediately when the bucket is empty
  }
}
```

For **Token Bucket**, `TokensPerPeriod == TokenLimit` keeps the long-run average equal
to the burst capacity (30 requests/minute per client). If `TokensPerPeriod` were
larger than `TokenLimit`, the average would exceed burst capacity.

Initial values were chosen for a dev/learning ecommerce API behind one server:
30 requests/minute per client is generous for a human-driven Angular UI while still
blocking scripted abuse; 1000 requests/minute server-wide protects the host without
throttling normal multi-user traffic. Tests override all values explicitly.

The strongly typed view is `RateLimitingOptions` (`GlobalRateLimitOptions` /
`ClientRateLimitOptions`) in `RateLimitingRegisteration.cs`, bound from the config
section via `GetSection("RateLimiting").Get<RateLimitingOptions>()`. The default
values in those classes are the fallback if the section is missing.

---

## 6. Why 429 is returned by the Rate Limiter, not thrown

A rate-limit rejection is a **response**, not an exception:

```
Rate limit exceeded
        ↓
Built-in Rate Limiter (short-circuits the pipeline)
        ↓
OnRejected callback
        ↓
429 ProblemDetails (+ Retry-After when available)
```

It is intentionally **not**:

```
Rate limit exceeded
        ↓
throw RateLimitExceededException
        ↓
GlobalExceptionHandler
        ↓
429
```

Reasons:

- The limiter short-circuits **before** controller/authentication execution, so there
  is no handler to run; throwing would only force an unrelated path to do work.
- No `RateLimitExceededException` exists (nothing was added to `Ecom.Core.Exceptions`
  and `GlobalExceptionHandler` was **not** touched).
- The rejection is a *state* ("back off now") rather than a failure of the request,
  which matches the middleware's own contract.

### 429 body and Retry-After

`OnRejected` builds a `ProblemDetails` exactly like `GlobalExceptionHandler`:

```json
{
  "type":   "about:blank",          // or an RFC 6585 URL if you prefer
  "title":  "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please retry later.",
  "instance": "/api/product/GetAll",
  "extensions": { "traceId": "..." }
}
```

Content-Type is `application/problem+json` and the status is `429 Too Many Requests`.
If the rejected lease exposes `MetadataName.RetryAfter`, the callback writes the
`Retry-After` header (whole seconds, using `Math.Ceiling`). For a sliding window that
is the time until a permit frees; for a token bucket it is the next replenishment.
When the limiter provides no retry metadata the header is simply omitted — the
frontend contract stays ProblemDetails-first, with `Retry-After` as an optimization.
Because `GlobalLimiter` also short-circuits, its 429 is produced by this same callback.

---

## 7. Middleware Ordering (final)

`Program.cs` pipeline (final state, net10.0):

```
1  UseExceptionHandler()          // outermost; owns every unhandled exception -> ProblemDetails
2  UseHangfireDashboard("/hangfire")
3  UseStaticFiles()               // serves wwwroot (imgs, etc.)
4  UseCors("AllowAngularApp")
5  (Dev) MapOpenApi / MapScalarApiReference      // mapped, not staged middleware
6  UseHttpsRedirection()
7  UseRateLimiter()               // NEW — global sliding window then per-client token bucket
8  UseAuthorization()
9  MapControllers()               // opt into per-client policy via [EnableRateLimiting]
```

Why the limiter sits at position 7:

- **After `UseExceptionHandler`** — the exception handler remains the outermost error
  owner; unhandled exceptions thrown downstream still become ProblemDetails.
- **After `UseCors`** — 429 responses must carry the CORS headers (e.g.
  `Access-Control-Allow-Origin`) for the Angular app to be able to read them. A
  limiter placed before CORS would emit header-less rejections that browsers hide.
- **After `UseHttpsRedirection`** — a plain-HTTP redirect is not counted against the
  quota; the followed HTTPS request is.
- **Before `UseAuthorization` and `MapControllers`** — every mapped endpoint is
  covered, and limiting is enforced before authentication/authorization work, which
  protects login-style endpoints from brute force.

Consequence (by design): static files and the Hangfire dashboard execute **before**
the limiter and are not rate-limited. They are low-ceremony server resources; the
API endpoints are the surface the rate limiter guards. Moving the limiter before
`UseCors` would cover them but break CORS on 429s, so the ordering is intentional.

---

## 8. Files and Folders

Created:

| Path | What / why |
|------|-----------|
| `Src/ECom.Api/Registerations/RateLimitingRegisteration.cs` | The one place that configures rate limiting: `RateLimitingConfigure(IServiceCollection, IConfiguration)` registers `AddRateLimiter` (GlobalLimiter + `per-client` policy + `OnRejected`). Also exposes public, testable `CreateGlobalLimiter` / `CreateClientLimiter` / `GetClientPartitionKey` and the config option classes. |
| `Src/ECom.Api.Tests/RateLimiting/RateLimitingTests.cs` | 8 tests: per-partition unit tests (global single-capacity, IP partition, UserId partition, key logic) + two in-process Kestrel integration tests (429 ProblemDetails, Retry-After). |

Modified:

| Path | What changed | Why |
|------|-------------|-----|
| `Src/ECom.Api/Program.cs` | Added `builder.Services.RateLimitingConfigure(builder.Configuration);` and `app.UseRateLimiter();` (position 7) + `using ECom.Api.Registerations;` | Registers the built-in limiter and inserts the middleware into the pipeline. |
| `Src/ECom.Api/appsettings.json` | Added the `RateLimiting` section (see §5) | All limits are config-driven per requirement. |
| `Src/ECom.Api/Controllers/ProductController.cs` | Class-level `[EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]` | Opts every product action into the per-client token bucket. |
| `Src/ECom.Api/Controllers/CategoryController.cs` | Class-level `[EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]` | Same for category actions. |
| `Src/ECom.Api.Tests/ECom.Api.Tests.csproj` | Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` | Lets the test project build/test the in-process Kestrel probe app (`WebApplication`) for end-to-end rate-limit assertions. No NuGet packages added. |
| `RATE_LIMITING.md` | Rewritten from "planned + custom middleware" to the implemented built-in design | Documents the final architecture. |

Deleted:

| Path | Why |
|------|-----|
| *(none)* | The previously planned custom `RateLimitMiddleware.cs`, `RateLimitRegistration.cs`, and middleware tests were **never created**. |

No custom algorithm, no `RateLimitMiddleware`, no runtime "built-in vs custom" switch
exists. The only implementation is ASP.NET Core's built-in rate limiter.

---

## 9. Testing

`Src/ECom.Api.Tests/RateLimiting/RateLimitingTests.cs` (8 tests, total suite now 74).

Determinism: partition-level tests use the **same** factory methods the middleware
uses (`CreateGlobalLimiter` / `CreateClientLimiter`) with `DefaultHttpContext`
(fake IPs + claims identities) — no real-time waiting and no server. Both integration
tests boot a minimal in-process Kestrel app that calls the **production**
`RateLimitingConfigure`, so they exercise the real middleware.

| Test | Verifies |
|------|----------|
| `GlobalLimiter_IsTrulyGlobal_...` | Three unrelated clients (different IPs, one authenticated) share one capacity; the third is denied — the global limiter is server-wide, not per-client. |
| `ClientLimiter_AnonymousRequests_ArePartitionedByIp` | Same IP drains one bucket (2 tokens → 3rd denied); a different IP still has a full independent bucket. |
| `ClientLimiter_AuthenticatedRequests_ArePartitionedByUserId_NotIp` | Two users behind the **same** IP have independent buckets; repeating the same user drains that user's bucket. |
| `ClientLimiter_AuthenticatedAndAnonymous_OnSameIp_HaveIndependentBuckets` | Authenticated uses `user:` key, anonymous uses `ip:` key — same IP still yields two buckets. |
| `GetClientPartitionKey_UsesUserIdWhenAuthenticatedAndIpOtherwise` | Key strings: `user:{id}` vs `ip:{ip}` vs `ip:unknown`. |
| `GetClientPartitionKey_AuthenticatedWithoutUserIdClaim_FallsBackToIp` | Defensive fallback. |
| `GlobalSlidingWindow_WhenExhausted_Returns429ProblemDetails` | End-to-end: 2 allowed, 3rd → `429`, `application/problem+json` body with `status=429`, `title="Too Many Requests"`. |
| `ClientTokenBucket_WhenExhausted_Returns429WithRetryAfter` | End-to-end: bucket of 2 exhausted → `429` with a positive `Retry-After` header + ProblemDetails body. |

The 66 pre-existing tests (controllers via mocks, repositories, services) are
unaffected — they invoke controllers directly and never pass through middleware.

---

## 10. Production Considerations

- **Reverse proxy / forwarded headers:** not configured today; `RemoteIpAddress` is the
  real peer. Before deploying behind a proxy or load balancer, enable
  `UseForwardedHeaders` (`ForwardedHeaders.XForwardedFor`) *before* `UseRateLimiter`,
  restrict to trusted `KnownProxies`/`KnownNetworks`, and then re-verify per-IP
  partitioning. Do not blindly trust arbitrary `X-Forwarded-For` headers.
- **In-memory state:** the sliding window and every token-bucket partition live
  **in process memory**. A restart (or multiple stopped/started replicas) resets all
  counters — this is fine for the current single-instance dev app.
- **Multiple instances / distributed limiting:** with more than one server instance,
  each instance enforces its own independent window, so effective capacity scales with
  the instance count and a client could multiply its quota across instances. Fixing
  that requires a **distributed** Rate-Limiting store (e.g. custom
  `PartitionedRateLimiter` backed by Redis/`IDistributedCache`). Explicitly **not
  implemented** — this design is single-instance by intent and should not be
  over-engineered for a hypothetical deployment.
- **Queue behavior:** `QueueLimit = 0` for both layers means **no queuing** — a request
  over the limit is rejected immediately with 429. This keeps latency flat under load
  and avoids head-of-line blocking. A small queue is a future option if short bursts
  should be absorbed instead of rejected.
- **Replenishment timers:** both limiters use `AutoReplenishment = true` (framework
  timers refill permits/buckets). Fine for one host; a distributed design would move
  replenishment to the shared store.
- **Not rate-limited:** static files and the Hangfire dashboard (positions 2–3, before
  the limiter) — intentional, see §7.