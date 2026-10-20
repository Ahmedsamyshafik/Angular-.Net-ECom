# ECom Backend — Change Inventory & Architecture Audit (Read-Only)

Audit date: 2026-10-12. Baseline compared: `HEAD (bc8520d "Alot of Features...")`.
Everything below is based on `git status`, `git diff HEAD`, `git show HEAD:...`, project files, DI registrations, and the current source. **No files were modified during this audit.**
Runtime checks from the earlier bug audit are reused only where they corroborate a change; nothing new was executed.

---

## 1) Current project structure and actual project-reference graph

Projects (net10.0), from `.slnx` and each `.csproj`:

```
ECom.Api.Tests ──▶ ECom.Api ──▶ ECom.Infrastructure ──▶ Ecom.Core
                                    (no reference beyond Core)
```

- `ECom.Api.csproj`: only ProjectReference → `..\ECom.Infrastructure\ECom.Infrastructure.csproj` (+ packages: Asp.Versioning.Mvc, FluentValidation, Hangfire, OpenApi, Scalar, EF Design, Redis).
- `ECom.Infrastructure.csproj`: only ProjectReference → `..\Ecom.Core\Ecom.Core.csproj` (+ EF Core/SqlServer/Tools, Identity, JwtBearer, Hangfire, Redis, MailKit-less; `Microsoft.Extensions.FileProviders.Abstractions`).
- `Ecom.Core.csproj`: **no ProjectReferences** (packages only: AutoMapper, MailKit/MimeKit, EF Design, Identity.Stores).
- `ECom.Api.Tests.csproj`: ProjectReference → `..\ECom.Api\ECom.Api.csproj` only.

Actual dependency direction: `Api → Infrastructure → Core`. There are **no circular project dependencies**. Core does not compile-against Infrastructure (no assembly reference).

Important nuance: `Src\Ecom.Core\Interfaces\IMyAuth.cs` contains `using ECom.Infrastructure.Repositories;` (header line 4, both at HEAD and now). This is a **dead using** — no type from that namespace is referenced in Core, so it compiles, but Core textually points at an Infrastructure namespace. Smell only, no reference.

---

## 2) Complete file-by-file change inventory (working tree vs HEAD)

All files below are **uncommitted** (modified M / deleted D / untracked ??).
Summary: `59 files changed, 980 insertions(+), 6426 deletions(-)` (the deletions are almost entirely the collapsed migration history).

### 2.1 — Product and Category

| File | Change | Original vs current | Why | Interface/contract/DB/validation/security/DI |
|---|---|---|---|---|
| `Src\Ecom.Core\DTO\Products\AddProductDto.cs`, `UpdateProductDto.cs` | +`Quantity` | Add/Update previously carried no stock. Now carries `Quantity`. | Enables stock validation in `GetProductForAddToCartAsync`. | Public request contract (new field; additive). |
| `Src\ECom.Api\DTO\AddProductFormDto.cs`, `UpdateProductFormDto.cs` (+ mappers) | +`Quantity` | Form DTOs now accept quantity. | Same reason, multipart path. | Request contract (additive). |
| `Src\ECom.Api\Validators\AddProductFormValidator.cs`, `UpdateProductFormValidator.cs` | + rule | `Quantity >= 0`. | Input guard. | Validation (additive). |
| `Src\ECom.Infrastructure\Application\Services\ProductService.cs` | `GetProductForAddToCartAsync` rewritten; `SystemUserId` int→string | Original: returned `null` for missing product, `FinalPrice = 0` when no discount, `PriceExpiresAt = first linked discount's EndDate`. Current: quantity<=0 → `BadRequest`, missing product → `GetByIdOrThrowAsync` (404), stock check → `BadRequest`, `FinalPrice = priceAfterDiscount ?? Price` (base when none), `PriceExpiresAt` only when a discount applies, `Max(EndDate)` over **all** linked discounts; photos filtered `IsDeleted != true`. | Stock/cart correctness + price semantics fix. | Business behavior + stock validation. Verified live (cart/order E2E) and by ProductServiceTests (203 green). |
| `ProductService.cs` — `AddAsync` | returns `AddProductResult(dto, storedPaths, entity)` | Now also returns the tracked entity. | Needed by the discount-on-add path to resolve generated FK via navigation. | Service return type (additive, `Entity = null` default). |
| `Src\Ecom.Core\DTO\Products\AddProductResult.cs` | +`TblProducts? Entity = null` | Result used to carry only DTO + paths. | Same as above. | Public return type (additive). |
| `Src\ECom.Infrastructure\Application\Orchestrators\AddProductOrchestrator.cs` | tries `result.Entity` branch first | Original always set `ProductIds = { result.Product.Id }` then `ApplyDiscountAsync`. Current prefers `ApplyDiscountToTrackedProductAsync(discount, result.Entity)`, falls back to old path. | Fixes 500/FK issue: DTO id is 0 until commit, so linking by function key was wrong. | Workflow/behavior + feature (discount-on-add). Verified by AddProductOrchestratorTests (green). |
| `ProductController.cs`, `CategoryController.cs` | **unchanged in working tree** (read endpoints + write endpoints + per-client rate limiting already at HEAD) | — | — | Note: these controllers carry **no `[Authorize]`** (pre-existing; confirmed in bug audit B1). |
| `Src\ECom.Infrastructure\Application\Services\CategoryService.cs` | `SystemUserId` const int→string | Aligns with BaseEntity change (see 2.6). | Required by the entity type change. | No public contract change. |

### 2.2 — Discounts and pricing

| File | Change | Original vs current | Why |
|---|---|---|---|
| `Src\Ecom.Core\Interfaces\IDiscountProductRepo.cs` | + overload `AddDiscountProductAsync(TblDiscount, TblProducts product, ...)` | New navigation-based link insert. | Lets EF insert product before join row and resolve the generated product id. |
| `Src\ECom.Infrastructure\Repositories\DiscountProductRepo.cs` | + impl of the new overload (sets `Product`, not `ProductId`) | New. | Same. |
| `Src\Ecom.Core\Services\IDiscountFacade.cs` | + `ApplyDiscountToTrackedProductAsync(AddDiscountDto, TblProducts, ...)` | New public facade method. | Exposes the tracked-product path to the orchestrator. |
| `Src\ECom.Infrastructure\Application\Facades\DiscountFacade.cs` | `ApplyCoreAsync` extracted into `BuildDiscount` + `ApplyCoreAsync`; `ApplyDiscountToTrackedProductAsync` added | Behavior of `ApplyDiscountAsync`/`ApplyCategoryDiscountAsync` unchanged; no direct Commit inside facade (never was). | Refactor enabling the tracked-product branch. Verified by DiscountFacadeTests (green). |

`TblDiscount`, `TblProductDiscount`, `DiscountType`, `DiscountPriceCalculator`, `DiscountConfig`, `DiscountRepo` — **unchanged**.

### 2.3 — Cart and Redis

| File | Change | Original vs current | Why |
|---|---|---|---|
| `CartController.cs` | `[Authorize]` added; all `userId` params removed; gets user id from `ClaimTypes.NameIdentifier` | Original: unauthenticated, userId came from query/body. Current: 401 without token; ownership always from token. | Security + ownership correctness. **Public contract change** (breaking): Get/Add/Delete/Update/UpdateCart no longer accept a userId payload. Verified live (401 without cookie; cart scoped to user). |
| `Src\ECom.Api\DTO\AddProductToCartDto.cs` | removed `UserId` | Payload no longer carries owner. | Same. Contract change. |
| `Src\Ecom.Core\DTO\CartDto\UpdateCartItemDto.cs` | `userId` removed (commented now) | — | Same. Contract change. |
| `Src\Ecom.Core\DTO\CartDto\UpdateCartRequest.cs` | `userId` now `string?`, assigned server-side in controller | — | Same. Contract change. |
| `CartProductFacade.cs` | `AddProductToCartAsync/UpdateCartItemAsync/UpdateCartAsync` rewritten | Original: hardcoded/placeholder cart ids (`"getting from user id "`, `"12"`), no stock check, `UpdateCartItemAsync` incremented quantity instead of setting it and nullable-product branch; `UpdateCartAsync` didn't remove qty<=0 lines. Current: uses the real user id, stock re-validated against resulting total, quantity<=0 removes lines, badges removed from `UpdateCartAsync` (null-filtered before removal). | Ownership + correctness fixes. Verified live (cart E2E). |
| `RedisCartRepository.cs` | unchanged | — | — |
| `infrastructureRegisteration.cs` | Redis config key `GetConnectionString("Redis")` → `configuration["Redis"]` | Reads the value from the same appsettings position; both resolve to `localhost:6379` on this machine. | Cleanup (behavior-neutral here). |

### 2.4 — Orders and inventory (NEW feature this session)

All untracked (`??`):
- `Src\ECom.Api\Controllers\Order\V1\OrderController.cs` — `[Authorize]`, `api/v{version}/Order`. `Create` = order → **Commit** → clear cart (the commit-then-clear order + `ClearUserCartAsync` calls are the two bug fixes of the session: previously the cart was deleted before any save, and the serialization cycle 500'd `Create`/`GetAllOrders`).
- `Src\ECom.Infrastructure\Application\Services\OrderService.cs` — `CreateOrderAsync`, `ClearUserCartAsync`, `GetAllOrdersForUserAsync`, `GetOrderById`, `GetDeliveryMethodAsync`, `GetAllDeliveryMethodAsync`, `BuildOrderItemsAsync`. Depends on `IMyAuth`, `IDeliveryMethodsRepo`, `ICartRepository`, `IOrdersRepo`, `IProductService`, `IMapper` (**not** on `IUnitOfWork`).
- `Src\Ecom.Core\Services\IOrderService.cs` — interface.
- Entities `TblOrders`, `TblOrderItem` (with `[JsonIgnore] tblOrder` — the serialization fix), `TblShippingAddress`, `TblDeliveryMethod`, `Status` — all in `Ecom.Core.Entities.Order`.
- Configs `OrderConfig.cs` (`OwnsOne shippingAddress`, `HasConversion status↔string`, decimal(18,2), cascade order items, `DeliveryMethodConfig.cs` seeds 3 methods ids 1–3 via `HasData`), `OrderItemConfig.cs`.
- Repos `OrdersRepo.cs` (2 queries: `GetUserOrdersByEmail`, `GeOrdersWithMethodDeliveryById`; **no `IsDeleted` filter**), `OrderItemsRepo`, `ShippingAddressesRepo`, `DeliveryMethodsRepo` (empty `GenericRepo` subclasses).
- `src\Ecom.Core\DTO\CartDto\Order\OrderDto.cs` (OrderDto, shipAdressDto, OrderResponse, DeliveryMethodResponse), `ShippingAddressMapping.cs`.
- DbSets added in `AppDbContext.cs`: `tblOrders`, `tblOrderItems`, `tblShippingAddresses`, `tblDeliveryMethods`.

Interface/DI effects: `IUnitOfWork` + `UnitOfWork` gain `IOrderService` (ctor param). `infrastructureRegisteration.cs` registers `IOrdersRepo`, `IOrderItemsRepo`, `IShippingAddressesRepo`, `IDeliveryMethodsRepo`, `IOrderService`, and (new) **`IMyAuth`** (`AuthReposiory`) — see sections 5/6.

Inventory: **stock is validated but never decremented** (confirmed; no code path reduces `TblProducts.Quantity`). New feature, 0 automated tests (only live E2E this session).

### 2.5 — Authentication and authorization

| File | Change | Effect |
|---|---|---|
| `Src\ECom.Infrastructure\Services\GenereateToken.cs` | uncommented `Claim(ClaimTypes.NameIdentifier, user.Id)` | JWT now carries the user id. Required by Cart/Order `[Authorize]` ownership. Token **contract change**; verified live (login + cart). |
| `Src\ECom.Infrastructure\Repositories\AuthReposiory.cs` | added `GetUserByEmail`; `SendEmailForForgetPassword` wrapped in try/catch returning `false` | New lookup for OrderService; swallow-all behavior change in the forget-password path (hides real failures, returns 400 with "something is wrong"). |
| `Src\Ecom.Core\Interfaces\IMyAuth.cs` | +`Task<AppUser> GetUserByEmail(string email)` | Interface addition. |
| `infrastructureRegisteration.cs` | `services.AddScoped<IMyAuth, AuthReposiory>();` (new) | DI addition. Now `IMyAuth` exists **both** in DI and as a manual `new AuthReposiory(...)` inside `UnitOfWork` (see 5/6). |
| `CartController`/`OrderController` | `[Authorize]` | See 2.3/2.4. |
| `ApiKey`? none. Program.cs unchanged (HSTS, SecurityHeaders, Hangfire dashboard, rate limiting, ProblemDetails, versioning all were already at HEAD). |

### 2.6 — UnitOfWork and repositories

| File | Change | Original vs current | Impact |
|---|---|---|---|
| `Src\ECom.Infrastructure\Repositories\UnitOfWork.cs` | **removed** `Task IUnitOfWork.Commit(...) { throw new NotImplementedException(); }`; added `OrderService` property + ctor param | **Head-critical:** at HEAD there were TWO members: `public async Task Commit` (real `SaveChangesAsync`) **and** an explicit `Task IUnitOfWork.Commit` that **threw NotImplementedException**. Because `MyBaseController` stores `IUnitOfWork _unitOfWork`, every `_unitOfWork.Commit(...)` in every controller bound to the EXPLICIT implementation → **HTTP 500 on every DB-writing endpoint (Product Add/Update/Delete/ApplyDiscount/ApplyCategoryDiscount, Category Add/Update/Delete)**. | The single largest behavioral fix of the session: committed writes actually persist now. Verified by 203 green tests + live E2E (product/category/discount writes, order persistence). |
| `Src\ECom.Infrastructure\Data\AppDbContext.cs` | +4 Order DbSets | New tables. | Schema. |
| `BaseEntity.cs` | `CreatedBy/UpdatedBy/DeletedBy` `T?` → `string?` | Breaking generic-entity type change. | Forced `SystemUserId` int→string in Product/Category services; DB column type in snapshot. |
| `GenericRepo.cs` | unchanged | — | Repos still **never** call `SaveChangesAsync` (only `UnitOfWork.Commit` does). |

### 2.7 — Migrations and EF model (**highest-risk area of the working tree**)

- **Deleted (D, 34 files):** all 17 previously-committed migrations (`20260811155140_init` … `20261008214706_addIdentityTables`) including `.Designer.cs`.
- **Modified (M):** `AppDbContextModelSnapshot.cs` (+378) — now reflects one combined model (Identity + products/categories/photos + discounts + orders + owned shipping address table `tblShippingAddresses`).
- **Untracked (??):** single squash migration `20261012120040_WhooooleUpdateee.cs(.Designer.cs)` that creates everything from scratch + seeds delivery methods (HasData 1–3).
- Timestamp = today (2026-10-12), name "WhooooleUpdateee".

Consequence: the repo now has **zero preserved migration history**; the new migration can only be applied to a **fresh** database. Applying it to the existing local DB (whose `__EFMigrationsHistory` still lists the old migrations) will collide. This looks like an intentional "squash for release" but is one-way and must be committed/documented deliberately.

### 2.8 — Tests and Postman

- `ECom.postman_collection.json` (M, +401): added **Account** (Register/Login/ActiveAccount), **Cart** (Get/Add/Update/UpdateCart/Delete), **Order** (CreateOrder/GetAllOrders/GetAllDeliveryMethod/GetDeliveryMethod) folders; existing Category/Product folders untouched.
- `ECom.postman_environment.json` (**??**, new): baseUrl/testEmail/testPassword.
- Tests: **no test files changed, added, or deleted** this session. `Src\ECom.Api.Tests` unchanged (203 tests). **No automated coverage for Cart, Order/OrderService, Auth/IMyAuth, DI, or the migrations.** Order validated only via live E2E.

### 2.9 — Other / unrelated

- `Src\ECom.Api\wwwroot\imgs\product\*.jpg` (20) and `imgs\category\*.jpg` (5) — **untracked** seed images uploaded this session via the API.
- `AUDIT_REPORT.md` (repo root, **??**) — the previous bug-audit deliverable.
- `.gitignore`/README/.gitattributes — committed earlier (no change here).

---

## 3) Highest-risk code changes to inspect manually

1. **UnitOfWork.Commit (removed the throwing explicit impl)** — the change that makes every committed write persist. Risk: previously "always 500"; now really writes. Nothing else saves. `GenericRepo` has no save. Verify no path double-commits or commits nothing.
2. **Migration collapse** (delete 17 + single squash + snapshot) — destroys history; fresh-DB-only. Decide and commit together with the new migration; do not apply to the existing DB.
3. **Cart/Order authorization + userId-from-claim + token `NameIdentifier` claim** — breaking public contract (clients can no longer pass userId). Requires the News token claim to always exist; ownership now depends on claim↔DB-user consistency (verified consistent: `GenereateToken` sets `NameIdentifier = user.Id`, cart key = that id, `OrderService` looks up user by email then keys cart by `user.Id`).
4. **BaseEntity CreatedBy `T→string`** — cascaded into Product/Category services; check snapshot column types vs DB.
5. **`GetProductForAddToCartAsync` now throws** `BadRequest` on quantity<=0 and insufficient stock; all callers (ProductService→cart facade, OrderService) rely on it; previously returned null/0.
6. **Discount-on-add navigation path** (`AddProductResult.Entity`, new `IDiscountProductRepo` overload) — EF inserts product before join row; verify with the 203 tests (Passing) + earlier live seeding.
7. **Order create commit-then-clear-cart** — deliberate non-atomicity with Redis (comment in code). If clear fails after commit, retry duplicates the order.
8. **Redis config key change** (`GetConnectionString("Redis")`→`configuration["Redis"]`) — same value on this host; if a connection-string was intended under the `ConnectionStrings` section, verify.

---

## 4) Actual call chains (from source, not from names)

**Product create + discount:**
`ProductController.Add (multipart) → AddProductOrchestrator.ExecuteAsync → ProductService.AddAsync [ImageManagementService.AddImagesAsync → ProductRepo.AddAsync(track)] → (if Discount) DiscountFacade.ApplyDiscountToTrackedProductAsync [DiscountRepo.AddAsync; DiscountProductRepo.AddDiscountProductAsync(entity, trackedProduct)] → controller UnitOfWork.Commit (AppDbContext.SaveChangesAsync)`; on any failure `AddProductOrchestrator.CompensateAsync → ImageManagementService.RemoveImagesAsync`.

**Product read + price:**
`ProductController.GetPage/GetById → ProductService.GetPageAsync/GetByIdAsync → ProductRepo → GetDiscountsByProductIdAsync → DiscountProductRepo.GetByProductIdsAsync (AsNoTracking, active-window NOT applied) → DiscountPriceCalculator.CalculatePriceAfterDiscount` (only producer of "active price").

**Discount apply (endpoint):**
`ProductController.ApplyDiscount → DiscountFacade.ApplyDiscountAsync → BuildDiscount + DiscountProductRepo.AddDiscountToProductsAsync (track) → controller Commit`.

**Add to cart:**
`CartController.AddCart → CartProductFacade.AddProductToCartAsync(userId, product, qty) → (existing line?) → ProductService.GetProductForAddToCartAsync [ProductRepo.GetByIdOrThrow → DiscountProductRepo → Calculator] → RedisCartRepository.GetCartAsync/SetCartAsync`. **No DB commit** (Redis only — correct).

**Create order:**
`OrderController.Create → UnitOfWork.OrderService.CreateOrderAsync(email, dto) [IMyAuth.GetUserByEmail → DeliveryMethodsRepo.GetByIdAsync → RedisCartRepository.GetCartAsync(user.Id) → BuildOrderItemsAsync (per line: ProductService.GetProductForAddToCartAsync) → OrdersRepo.AddAsync(track order+address+items)] → controller UnitOfWork.Commit → UnitOfWork.OrderService.ClearUserCartAsync [IMyAuth.GetUserByEmail → RedisCartRepository.DeleteCartAsync]`.

**Get my orders / delivery method:**
`OrderController.GetAllOrdersForUser/GetDeliveryMethod → OrderService → OrdersRepo.GetUserOrdersByEmail / GeOrdersWithMethodDeliveryById (both Include deliveryMethod + orderItems; no IsDeleted filter)`.

**Authenticate:**
`AccountController.Login → UnitOfWork.myAuth (manual AuthReposiory) .Login [UserManager.FindByEmailAsync; SignInManager.CheckPasswordSignInAsync; GenereateToken.GetAndCreateToken (claims: Name, Email, NameIdentifier)] → Response.Cookies.Append("token", …, Domain=localhost, HttpOnly, SameSite=Strict, 1 day)`.
Register/Forget/Reset/Active/Change go through the **same manual** AuthReposiory on `UnitOfWork.myAuth`; **OrderService uses the DI `IMyAuth`** (different instance).

---

## 5) Architecture findings (responsibilities, repo/service, UnitOfWork, DI)

**A1 — `UnitOfWork` is a wide service locator. (Design concern)**
`UnitOfWork` (UnitOfWork.cs:1–84) exposes `ProductService`, `CategoryService`, `DiscountFacade`, `AddProductOrchestrator`, `CartProductFacade`, `EmailService`, `GenerateToken`, `myAuth`, `OrderService`, and internally holds `UserManager`/`SignInManager`. Every controller routes all application logic through it; a controller only ever sees `_unitOfWork`. Consequence: everything must be resolvable to build UnitOfWork, the type's construction graph is huge, and controllers gain accidental coupling to everything. It is an existing pattern (pre-dates this session) and works; not a defect.

**A2 — `IMyAuth` exists twice per scope: DI instance + manual `new`. (Confirmed, low-impact)**
`infrastructureRegisteration.cs:58` now registers `IMyAuth → AuthReposiory` (added this session), while `UnitOfWork` ctor (line 69) still does `myAuth = new AuthReposiory(userManage, signInManager, generateToken, emailService)`. Both wrap the same scoped `AppDbContext`/`UserManager`/`SignInManager` (those are injected, not re-created), and `AuthReposiory` has no instance state of its own, so **no correctness bug and no lifetime mismatch** — but `AccountController` uses the manual instance while `OrderService` uses the DI instance: two objects where one should exist. Verified registrations + usages (grep). It is unnecessary duplication/coupling, not a state or lifetime defect.

**A3 — `UnitOfWork` now depends on a domain service (`IOrderService`). (Design concern)**
This session threaded `IOrderService` through `IUnitOfWork`/`UnitOfWork` (IUnitOfWork.cs:18, UnitOfWork.cs:38,57,70). `OrderService` itself does **not** reference `IUnitOfWork` (verified — no circular dependency, no captive dependency), and the app resolves/runs. But the persistence abstraction now hosts a business service; `OrderController.Create` uses it as a pass-through. Optionally inject `IOrderService` directly into `OrderController` (which already injects `IUnitOfWork` only, via `MyBaseController`).

**A4 — Core carries a dead `using ECom.Infrastructure.Repositories;`. (Confirmed cleanliness)**
`IMyAuth.cs:4`. No type used; compiles only because nothing from that namespace is referenced. Misleading if someone later expects to use Infrastructure types from Core.

**A5 — Dead/contradictory Order plumbing. (Confirmed cleanliness)**
`IOrderItemsRepo` and `IShippingAddressesRepo` are registered (infrastructureRegisteration.cs:48-49) and implemented but **never used** (grep: only registration lines). `AppDbContext` declares `DbSet<TblShippingAddress>` while `OrderConfig` maps it with `OwnsOne` to its own table (`tblShippingAddresses`, snapshot) — contradictory declarations that currently coexist because the owned config wins and the DbSet is never queried. Not a runtime failure (app builds/runs; E2E created an order), but confusing.

**A6 — `AuthReposiory` lives in `Repositories`. (Cosmetic)**
It is an application/auth service (uses UserManager/SignInManager) but sits in the `Repositories` namespace/folder. Existing pattern; not a defect.

**A7 — Controllers hold workflow/transaction coordination. (Design concern)**
Commit placement and compensation (`try/catch` → `CompensateAsync` in Add/AddCategory; `Create` = Create→Commit→ClearCart) live in controllers (ProductController.cs:85-93, CategoryController.cs:78-86, OrderController.cs:26-40). Consequence: workflows can't be unit-tested without HTTP; today it works and is explicit. Reasonable for this size; not recommended to introduce an Application layer/MediatR to "fix" it (see A9).

**A8 — Repositories are pure data access. (Positive, verified)**
`GenericRepo` only tracks/reads; `OrdersRepo` adds two Include-queries. No repo writes to `SaveChanges` (grep showed the **only** `SaveChangesAsync` call is `UnitOfWork.cs:79`). Repository layer is clean.

**A9 — Discount responsibility boundaries (concern C). (Defensible, small cleanup recommended)**
Verified facts from source:
- `IDiscountRepo` (discount master CRUD) is used **only** by `DiscountFacade` (DiscountFacade.cs:17,55,84). `ProductService` does **not** use `IDiscountRepo` — neither at HEAD nor now (git show).
- `ProductService` depends on `IDiscountProductRepo` (**join** repo) through a single private helper `GetDiscountsByProductIdAsync`, and delegates all eligibility/price math to `IDiscountPriceCalculator` (the one definition of "active"). Order flow reuses the same `ProductService.GetProductForAddToCartAsync` → same calculator.
- Discount **creation/assignment** is entirely `DiscountFacade`; the add-product branch routes through it via the orchestrator.
- Therefore: (1) calculation/eligibility already has a single owner (`IDiscountPriceCalculator`); (2) `ProductService`'s only "excessive" knowledge is *reading discount links* through a persistence repo — it does **not** know discount persistence/writing; (3) `DiscountFacade` already owns the write-side responsibility; (4) duplication is limited to re-loading links in three read paths, all funneled through one private method; (5) a minimal improvement is to move `GetDiscountsByProductIdAsync` behind `DiscountFacade` (or a tiny service) so ProductService doesn't touch a repo — **no new abstraction layer or MediatR is justified**; there is no circular-dependency risk either way.

**A10 — Response contract mixing. (Confirmed, low)**
Successful payloads use `responseApi`; errors use ProblemDetails (GlobalExceptionHandler). Pre-existing; low impact for a portfolio API.

**A11 — Soft-delete semantics uneven on new entities. (Design concern)**
`GenericRepo.GetAll*/GetById*` filter `IsDeleted == false`, but the new `OrdersRepo` queries (`OrdersRepo.cs:17-24`) do not. No order-delete path exists yet, so latent only; when (if) orders become soft-deletable, this must be revisited.

**A12 — Authenticated-rate-limit coverage. (Design concern)**
`[EnableRateLimiting]` is on Product/Category only; Cart/Order/Account rely on the global 1000/60s window (Program.cs). Pre-existing except that Cart/Order are new endpoints under the same rule.

---

## 6) Persistence and transaction-boundary findings

**P1 — Persistence is centralized: verified.**
Only `UnitOfWork.Commit` calls `SaveChangesAsync` (grep). Controllers call it exactly once per DB-writing endpoint: ProductController (87 Add, 104 Update, 113 Delete, 124 ApplyDiscount, 135 ApplyCategoryDiscount), CategoryController (80/97/106), OrderController (34). CartController performs Redis-only writes and never commits — correct, since the cart path touches no EF entities.

**P2 — The HEAD write path was completely broken (fixed this session).**
At `bc8520d`, the explicit `IUnitOfWork.Commit` threw `NotImplementedException` while the public `Commit` saved. Controllers called through the interface → **every DB-writing endpoint threw 500**. This explains the earlier "order not persisted" symptom and is the highest-value fix in the tree. Confirmed via `git show HEAD:…UnitOfWork.cs` + current controllers.

**P3 — Order create is intentionally not atomic with Redis. (Design risk, low)**
`Create → Commit → ClearUserCartAsync`. If the Redis delete fails **after** the DB commit, the user can resubmit and create a duplicate order. The code comments call this out deliberately (a failed save never wipes the basket). Acceptable trade-off; document for consumers.

**P4 — No multi-write transaction needed today.**
Each request performs one `SaveChangesAsync` inside an implicit transaction (order + orderItems + owned address + future stock decrement — all in a single save). Adding inventory decrement later inside `BuildOrderItemsAsync`/the same save is **naturally atomic**; explicit `TransactionScope` is not required.

**P5 — Discount FK failure surfaces as 500 (already in bug audit B2).**
`ApplyDiscountAsync` with a non-existent product id → tracked link → `Commit` throws `DbUpdateException` → default ProblemDetails 500 (reproduced live; nothing persisted). Fix = existence validation before tracking.

**P6 — File-store vs DB ordering.**
Product create writes image files to `wwwroot` before `Commit`; compensation removes them on failure. Between the file write and `Commit` no DB row exists yet, so a crash leaves orphan files (cleaned by the Hangfire purge job). Acceptable for this scope.

---

## 7) Prioritized future work (dependencies)

1. **[Critical, security]** Authorize Product & Category writes (read endpoints may stay public) — independent; also `ChangePassword` (see bug audit). Do before any public/portfolio use.
2. **[High, data]** Stock decrement on order creation (inside `BuildOrderItemsAsync`, same `SaveChanges` — atomic per P4) + add an integration (or E2E) test proving it. Depends on: Order flow (exists).
3. **[Medium]** `ApplyDiscount`/`ApplyCategoryDiscount` existence validation → 400/404 instead of 500. Independent.
4. **[Medium]** Make Add/Update responses reflect the post-commit entity (map after `Commit`) — ProductService/CategoryService + controllers. Independent.
5. **[Medium]** DI cleanup: inject `IMyAuth` into `UnitOfWork` (drop the `new`), remove dead registrations (`IOrderItemsRepo`, `IShippingAddressesRepo`, the `TblShippingAddress` DbSet or the `OwnsOne` mapping), drop the dead `using` in Core, optionally move `IOrderService` out of `IUnitOfWork` into controller injection. Depends on: tests around OrderController (none today) — add first.
6. **[Medium, repo]** Decide the migration squash: keep as the single initial migration and document "fresh DB only", or restore history. Blocks nothing for running/experimenting; matters for anyone else applying migrations.
7. **[Low]** `OrdersRepo` IsDeleted filtering; `PriceExpiresAt = Max(EndDate)` over all (incl. inactive) linked discounts (use only the winning discount's window); `GenereateToken` DateTime.Now → UTC; per-client rate limiting for Cart/Order/Account; `SendEmailForForgetPassword` hiding exceptions.
8. **[Process]** Add automated tests for Cart, Order/OrderService, Auth, and the migration/snapshot (SQLite/InMemory) before further refactors; run the new Postman collection via `newman`.

---

## 8) Areas that cannot be verified from available evidence

- **Current local DB vs the squash migration**: the app runs/writes against the existing DB (works), but whether its `__EFMigrationsHistory`/schema matches the new single migration is not established. Do not apply `20261012120040_WhooooleUpdateee` to an existing DB without checking.
- **Exact origin of each uncommitted hunk**: git proves the aggregated working-tree diff vs `bc8520d`, but cannot prove which individual edits pre-dated the session. Attribution to this session is based on session records/E2E/migration timestamp; treat "pre-existing" claims as estimates.
- **Real email flow** (Register → mail → ActiveAccount): DTO paths exist; dev DB used `EmailConfirmed=1` workaround.
- **Production Hangfire dashboard authorization** (default local-only filter assumed).
- **Token timezone behavior** on non-UTC hosts (`DateTime.Now`).
- **EF tolerance of `DbSet<TblShippingAddress>` + `OwnsOne`**: it builds/runs here, but the edge behavior on migrations/tools is untested.

---

## Final distinction

**Confirmed bugs already identified in the previous bug audit (unchanged/unfixed):**
- B1 Critical — anonymous Product/Category writes.
- B2 Medium — `ApplyDiscount` bad id → 500 (reproduced; nothing persisted).
- B3 High — stock never decremented on order.
- B4/B5 Medium — raw `TblOrders` return + stale add/update DTOs (mapped pre-commit).
- B8 Low/Medium — anonymous `ChangePassword` with null-user 500 risk.
- B6/B7/B9/B10 Low — `PriceExpiresAt` window, orders soft-delete filter, rate-limit coverage, token local time + duplicate `AuthReposiory`.

**Newly discovered confirmed bugs (this audit):**
- N1 (Critical-at-HEAD, now fixed): `UnitOfWork` explicit `IUnitOfWork.Commit` threw `NotImplementedException` overriding the working public `Commit` → every DB-writing endpoint returned 500 at `HEAD`. Removed this session; verified fixed.
- N2 (Cleanliness): `IShippingAddressesRepo`/`IOrderItemsRepo` registered but unused; `DbSet<TblShippingAddress>` contradicts `OwnsOne`.
- N3 (Cleanliness): dead `using ECom.Infrastructure.Repositories;` in Core `IMyAuth`.
- N4 (Behavior change): `SendEmailForForgetPassword` now swallows all exceptions → returns `false`; real failures are indistinguishable from "no user".
- N5 (Repo risk): migration history collapsed to one untracked squash migration (17 deleted); fresh-DB-only.

**Architecture / design concerns:** A1 (service-locator UnitOfWork), A3 (`IOrderService` inside UoW), A7 (transaction orchestration in controllers), A10 (mixed response contract), A11 (uneven soft-delete), A12 (rate-limit coverage), P3 (non-atomic Redis clear after order).

**Optional improvements:** A2→5 noted paths (DI dedup, dead-code removal, `GetDiscountsByProductIdAsync` behind DiscountFacade), P6 (nothing required), docs/Postman/newman, test coverage for Cart/Order/Auth.

**Positive / verified good:** single `SaveChanges` choke point (P1), pure data-access repositories (A8), single-source pricing in `DiscountPriceCalculator` (A9), no circular project or runtime DI dependencies, 203 tests + live E2E green.