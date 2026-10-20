# ECom Backend — Audit Report (read-only)

Scope: ECom.Api / Ecom.Core / ECom.Infrastructure (`Src\ECom.Api\ECom.Api.slnx`, .NET 10).
No source files were modified during this audit.

## Verification run (actually executed)
- `dotnet build` -> Build succeeded, 0 errors (only pre-existing nullable/analyzer warnings).
- `dotnet test` -> 203 passed / 0 failed / 0 skipped.
- Live API (http://localhost:5090) targeted probes (non-persisting / read-only); API stopped afterward. No files modified, no data reset.

---

## 1) Confirmed remaining bugs

### B1 — Product & Category write endpoints are anonymous — Critical (security)
- File: `Src\ECom.Api\Controllers\Product\V1\ProductController.cs` (class line 26) and `Category\V1\CategoryController.cs` (class line 24). Neither has `[Authorize]`; `Program.cs` sets no global/fallback auth policy.
- Scenario: call any write without a token.
- Expected: 401. Actual: reached.
- Reproduced: `POST /api/v1/product/ApplyDiscount` (no cookie, invalid body) -> 400 validation (not 401); `POST /api/v1/product/Add` (no cookie) -> 400 validation; `GET /product/GetAll` -> 200. Cart/Order without cookie correctly -> 401.
- Impact: anyone can create/update/delete products & categories and apply discounts. Highest-priority item for a public repo.

### B2 — ApplyDiscount to a non-existent product id -> HTTP 500 — Medium
- File: `DiscountFacade.ApplyCoreAsync` -> `DiscountProductRepo.AddDiscountToProductsAsync` (`DiscountProductRepo.cs:41`). No existence check; FK `FK_TblProductDiscounts_TblProducts_ProductId` (confirmed in `sys.foreign_keys`, `AppDbContext.cs:35`).
- Scenario: `POST /product/ApplyDiscount` with `ProductIds:[999999]`, valid window.
- Expected: 400/404. Actual: generic 500 (`DbUpdateException` -> `GlobalExceptionHandler` default branch).
- Reproduced: returned 500; DB delta confirmed nothing committed (discount count stayed 8; no `AUDIT-FK-TEST` row; no link).

### B3 — Orders do not decrement product stock — High (data consistency)
- File: `OrderService.CreateOrderAsync` / `BuildOrderItemsAsync` (`OrderService.cs:32,135`). Stock is validated but `product.Quantity` is never reduced on any path.
- Scenario: create two orders each for the full stock of a product.
- Expected: stock reduced (or explicit reservation). Actual: unchanged -> unlimited overselling.
- Inspection (definitive — no quantity update anywhere in the order path). May be deliberate scope, but is a real consistency gap.

### B4 — CreateOrder response leaks the entity and is contract-inconsistent — Medium
- File: `OrderController.Create` returns raw `TblOrders` (`OrderController.cs:33`), while `GetAllOrders` returns the `OrderResponse` DTO.
- Reproduced earlier E2E: body exposes `rowVersion`, `createdBy` (user GUID), base fields, and `"status":0` (numeric enum), whereas `GetAllOrders` returns `"status":"Pending"` (string).

### B5 — Add/Update responses are stale (mapped before Commit) — Medium
- Files: `ProductService.AddAsync/UpdateAsync`, `CategoryService.AddAsync/UpdateAsync` (DTO mapped from the tracked entity before `SaveChanges`).
- Actual: `POST /product/Add` returns `id:0`; Update returns pre-save `rowVersion`/photos. Reproduced (earlier E2E saw `id:0`).

### B6 — PriceExpiresAt may exceed the applied discount window — Low
- File: `ProductService.GetProductForAddToCartAsync` (`ProductService.cs:132-134`): `discounts?.Max(d => d.EndDate)` runs over all linked discounts (including inactive/future), not only the one that produced `FinalPrice`. Display-only; order pricing re-validates.

### B7 — Orders repo ignores soft-delete flag — Low
- File: `OrdersRepo.GetUserOrdersByEmail` / `GeOrdersWithMethodDeliveryById` (`OrdersRepo.cs:17-24`) lack `IsDeleted == false` (no global query filter exists in `AppDbContext`). No order-delete path exists today; impact latent.

### B8 — Account/ChangePassword is anonymous and can 500 — Low/Medium
- File: `AccountController.ChangePassword` (`AccountController.cs:85`) has no `[Authorize]`; `AuthReposiory.ChangePassword` calls `ChangePasswordAsync(user, ...)` where `user` may be null -> unhandled 500. Requires the old password. Inspection.

### B9 — Rate limiting not applied to Cart/Order/Account per-client — Low
- Only Product/Category carry `[EnableRateLimiting(per-client)]`; Cart/Order/Account rely solely on the global 1000/60s window. Inspection.

### B10 — Token uses local time; UnitOfWork builds a second AuthReposiory — Low
- `GenereateToken` uses `DateTime.Now` for `NotBefore/Expires` (validation has `ClockSkew=Zero`) — timezone-sensitive; not reproduced (login works on this host, likely UTC). `UnitOfWork` ctor `new AuthReposiory(...)` (`UnitOfWork.cs:69`) while `IMyAuth` is also DI-registered -> two instances (smell, not a defect).

### Verified NOT bugs (checked so they are not reported falsely)
- `PATCH /Cart/UpdateCart` with `{}`/no body -> 400/415 (ASP.NET implicit non-nullable validation + media-type), not an NRE 500.
- Unversioned routes: `GET /api/Cart/Get` and `/api/product/GetAll` -> 404 (inherited base `[Route("api/[controller]")]` is inert under versioning). No route leak.
- `GlobalExceptionHandler` does not leak stack/SQL details; maps NotFound/BadRequest/Conflict/Concurrency/BadHttpRequest/ArgumentOutOfRange correctly.

---

## 2) Previously fixed bugs — regression check
All still correct, no regressions found:
- `[JsonIgnore]` on `TblOrderItem.tblOrder` (`TblOrderItem.cs`) -> CreateOrder/GetAllOrders serialize (build + tests green; earlier live 200s).
- Order persistence + commit-then-clear-cart (`OrderController.cs:26`, `OrderService.ClearUserCartAsync`) -> order id 1 persisted with linked items; cart cleared after save.
- Stock guard, cart facade correctness, auth claims/NameIdentifier, add-time discount FK fix -> 203 tests green; live probes consistent.
- `UnitOfWork.Commit`, `GenericRepo.AddAsync` tracking model, `DiscountPriceCalculator` single-source window -> unchanged and consistent.

---

## 3) Incomplete / not fully verified
- Automated tests: none exist for Cart, Order/OrderService, CartProductFacade, Account/Auth, and the discount-apply FK path (existing tests: Product/Category services+controllers, Discount calculator/facade, mappings, repos, rate limiting, security headers, exception handler).
- Postman: Account/Cart/Order folders + environment were added, but not executed via newman.
- Email confirmation: bypassed in dev (DB EmailConfirmed=1); real activation flow not exercised.
- OrderService.GetOrderById exists but is not exposed by any endpoint.
- Stock on order (B3): confirmed unimplemented, not a partial implementation.

---

## 4) Readiness for GitHub portfolio release
Not yet. Buildable, test-green, Cart/Order happy paths work — close, but two items must be fixed first.

Must fix before release
1. B1 (Critical): add `[Authorize]` (or an admin/role policy) to Product & Category controllers; keep only reads anonymous if intended.
2. B3 (High): implement stock decrement on order (transactional with the order save) or explicitly document it as out of scope.

Strongly recommended next
3. B2 (Medium): validate product existence before creating discount links -> return 400/404 instead of 500.
4. B4/B5 (Medium): return DTOs (not raw entities) and rebuild the response after Commit so created/updated resources carry their real Id/version.

Nice-to-have: B6-B10; add Cart/Order/Auth automated tests; run the Postman collection through newman.

Unverifiable live (inspection-only/uncertain, not confirmed bugs): email activation, production Hangfire authorization, non-UTC host timezone behavior.
