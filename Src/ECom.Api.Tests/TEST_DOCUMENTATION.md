# ECom.Api.Tests — Complete Test Suite Documentation

This document is a study guide for the `ECom.Api.Tests` project. It explains the
framework, infrastructure, every test file, every individual test, and the
production code patterns they verify.

---

## 1. Project Overview

**Purpose:** Unit / integration tests for the **ECom.Api** ASP.NET Core Web API
(e-commerce backend: Products, Categories, Discounts, Photos).

**Test framework:** xUnit 2.9.3
**Mocks:** Moq 4.20.72
**DB testing:** EF Core In-Memory + SQLite (for the repository test)
**Job scheduling:** Hangfire with `Hangfire.MemoryStorage` (for image purge tests)
**AutoMapper:** real, generated from the API's mapping profiles

### Test project file (`ECom.Api.Tests.csproj`) key facts

| Item | Value | Why it matters |
|---|---|---|
| `TargetFramework` | `net10.0` | Matches the API project |
| `ImplicitUsings` | enabled | `System`, `System.Linq`, etc. auto-imported |
| `Nullable` | enabled | Compiler helps catch null bugs |
| Global using | `Xunit` added via `<Using Include="Xunit" />` | No need to write `using Xunit;` in every file |
| Package `Moq` | 4.20.72 | Mocking interfaces/DTOs/entities |
| Package `Microsoft.EntityFrameworkCore.InMemory` | 10.0.10 | Fake DB for repository test |
| Package `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.10 | (available; used as alternative to InMemory) |
| Package `Hangfire.MemoryStorage` | 1.8.1.2 | In-memory Hangfire storage so `BackgroundJob.Schedule` works in tests |
| `coverlet.collector` | 6.0.4 | Code coverage collection |
| Project reference | `ECom.Api` | Tests exercise the real API classes |

> Note: The test project references **ECom.Api** only; the API project itself
> references the Core and Infrastructure layers, so transitively everything is available.

### How to run the tests

```powershell
dotnet test Src\ECom.Api.Tests\ECom.Api.Tests.csproj
```

With verbose output:

```powershell
dotnet test Src\ECom.Api.Tests\ECom.Api.Tests.csproj --logger "console;verbosity=detailed"
```

With coverage:

```powershell
dotnet test Src\ECom.Api.Tests\ECom.Api.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## 2. Test files at a glance

| # | File | What it tests | Style |
|---|---|---|---|
| 1 | `ProductControllerTests.cs` | `ProductController` (18 tests) | Mock-based unit tests |
| 2 | `CategoryControllerTests.cs` | `CategoryController` (11 tests) | Mock-based unit tests |
| 3 | `FormDtoExtensionsTests.cs` | Form `ToDto()` conversion logic (9 tests) | Pure unit tests |
| 4 | `MappingConfigurationTests.cs` | AutoMapper profiles / RowVersion handling (5 tests) | Real AutoMapper |
| 5 | `ExceptionHandlingMiddlewareTests.cs` | Global exception middleware (3 tests) | `DefaultHttpContext` |
| 6 | `Services/ImageManagementServiceTests.cs` | Image file service, Hangfire purge (13 tests) | Real FS + mocks |
| 7 | `Repositories/RepositoryTests.cs` | Real EF repository against InMemory DB (1 test) | Integration-style |

Total: **~60 tests**

---

## 3. Shared concepts / test vocabulary

### 3.1 The `responseApi` wrapper

Every controller action returns an `Ok(new responseApi(...))` (or other status).
`responseApi` has three members:

```csharp
public int StatusCode { get; set; }
public string? StatusMessage { get; set; }
public object? Data { get; set; }
```

- `StatusCode` => HTTP-like code (200, 400, 404, 409...)
- `StatusMessage` => human text; defaulted from the status code if not provided
- `Data` => the payload (DTOs, lists, etc.)

**Why tests assert on `responseApi`:** Many tests do the following pattern:

```
var result = await controller.X(...)
var ok = Assert.IsType<OkObjectResult>(result);   // 1. HTTP layer: controller returns 200 OK
var api = Assert.IsType<responseApi>(ok.Value);    // 2. API layer: body is a responseApi
Assert.Equal(200, api.StatusCode);                  // 3. responseApi carries status 200
Assert.Same(dtos, api.Data);                        // 4. and contains the expected payload
```

The same composes for `NotFoundObjectResult` (404), `BadRequestObjectResult`
(400), and `ConflictObjectResult` (409).

### 3.2 Mocking `IUnitOfWork`

`IUnitOfWork` is a facade exposing repositories and services. In the test
constructor it is wired up so each property returns a dedicated dummy:

```csharp
_unitOfWork.Setup(u => u.ProductRepo)       .Returns(_productRepo.Object);
_unitOfWork.Setup(u => u.CategoryRepo)      .Returns(_categoryRepo.Object);
_unitOfWork.Setup(u => u.PhotoRepo)         .Returns(_photoRepo.Object);
_unitOfWork.Setup(u => u.ImageManagementService).Returns(_imageService.Object);
_unitOfWork.Setup(u => u.DiscountRepo)      .Returns(_discountRepo.Object);
_unitOfWork.Setup(u => u.DiscountProductRepo).Returns(_discountProductRepo.Object);
```

Then the real `ProductController(_unitOfWork.Object, _mapper.Object)` is created.

**Patterns used with Moq:**

| Moq construct | Meaning |
|---|---|
| `Setup(r => r.AddAsync(...)).ReturnsAsync(x)` | make the method return `x` |
| `Setup(r => r.GetByIdAsync(It.IsAny<object[]>())).ReturnsAsync(product)` | match **any** args |
| `Setup(...).ThrowsAsync(new DbUpdateConcurrencyException(...))` | make the call throw |
| `Verify(m => m.Method(args), Times.Once)` | assert the method was called exactly once |
| `Times.Never` | assert the method was not called |
| `Callback<UpdateProductDto, TblProducts>((src, dest) => ...)` | run side effects on call |
| `It.Is<IEnumerable<int>>(ids => ids.Contains(1))` | assert the actual argument content |

### 3.3 Why specific assertions are chosen

- `Assert.Same(dtos, api.Data)` — proves the controller passes through the same
  object instance the mapper returned (not a copy).
- `Assert.Equal(clientRowVersion, product.RowVersion)` — proves the **client
  byte[]** was attached to the tracked entity (concurrency token handling).
- `Assert.ThrowsAsync<T>` — proves an exception **propagates** (middleware only
  swallows concurrency exceptions).

---

## 4. `ProductControllerTests.cs` (18 tests)

Covers route `api/product` — `ProductController` in
`Src\ECom.Api\Controllers\ProductController.cs`.

### Fixtures

- `SampleProduct()` → a `TblProducts` with Id=1, Price=999.99, CategoryId=1,
  `RowVersion = [1,2,3]`, and one `TblPhoto` (Id=10, path `imgs/product/a.png`).
- `SampleDto()` → a `ProductDto` mirroring the product (CategoryName="Electronics",
  empty `PhotoUrls`).

The controller under test is created once per test (per constructor run) because
xUnit creates a new instance of the test class for every test method.

### 4.1 `GetAll` (1)

**`GetAll_ShouldReturnOkWithMappedProducts`**
- Mocks `ProductRepo.GetAllAsync(...)` returns `[SampleProduct()]`.
- Mocks `IMapper.Map<List<ProductDto>>(...)` returns the same dto list.
- `Act:` `controller.GetAll()`.
- `Assert:` result is `OkObjectResult`; body is `responseApi` with StatusCode 200
  and `Data` is **the same list instance** (`Assert.Same`).

> Guards the happy path of list retrieval (repo + mapper + wrapper).

### 4.2 `GetById` (2)

**`GetById_WhenProductExists_ShouldReturnOk`**
- `GetByIdAsync` returns `SampleProduct()`; mapper returns `SampleDto()`.
- Asserts OK + 200 + `api.Data.Id == 1`.

**`GetById_WhenProductMissing_ShouldReturnNotFound`**
- `GetByIdAsync` returns `null`.
- Asserts `NotFoundObjectResult` + responseApi 404.

> Covers the null-guard branch of the action.

### 4.3 `Add` (2)

**`Add_ShouldAddProductSaveImagesAndCommit`**
Form has **no discount fields** and no files.
- Mapper produces a `TblProducts` (Id=5).
- `AddImagesAsync` returns `["imgs/product/x.png"]`.
- `AddAsync` returns the product as-is; `GetByIdAsync` on category returns a
  `TblCategories` (Id=3).
- Asserts OK/200 and verifies method call counts:
  - `AddImagesAsync` called once with `("...", "product")`
  - `ProductRepo.AddAsync` once
  - `UnitOfWork.Commit()` once

> Exercises: form → dto → entity → save images → add → commit → map back → load category name.

**`Add_WhenDiscountPosted_ShouldAlsoApplyDiscount`**
Form **includes discount fields** (`DiscountFrom/To`, Percentage 10, "New Year").
- Discount repo `AddDiscountAsync` returns `TblDiscount { Id = 7 }`.
- Discount-product repo `AddDiscountToProductsAsync` returns `Task.CompletedTask`.
- Asserts OK/200 and verifies both discount calls happened exactly once.

> Exercises the `if (productDto.Discount != null)` branch → `ApplyDiscountInternalAsync`.

### 4.4 `Update` (3)

**`Update_ShouldReturnOkAndAttachClientRowVersionOntoEntity`**
- `GetByIdAsync` returns the tracked `product`.
- Mapper `Map(dto, entity)` uses a `Callback` that copies
  Name/Description/Price/CategoryId onto the entity (mirrors real AutoMapper).
- `AddImagesAsync` → empty; `HandleUpdatedImgs` → completed; category lookup returns
  `TblCategories { Id = 1 }`.
- Form carries `RowVersion = Convert.ToBase64String([9,8,7])`.
- Asserts:
  - OK/200
  - entity `Name == "Updated Laptop"`
  - entity `RowVersion == [9,8,7]` (client token wins)
  - `HandleUpdatedImgs` called with `(ids, paths, productId=1, null, "1")`
  - `Commit()` once

> This is the flagship concurrency-token test: **the DB token must equal the client token**.

**`Update_WhenProductMissing_ShouldReturnNotFound`**
- `GetByIdAsync` returns null → `NotFoundObjectResult` + 404.

**`Update_WhenConcurrencyConflict_ShouldReturn409`**
- `Commit()` throws `DbUpdateConcurrencyException`.
- Asserts `ConflictObjectResult` with StatusCode 409 **and** responseApi 409.

> In the real controller the catch maps the EF concurrency exception to 409
> ("The record was modified by another user. Please reload and try again.").

### 4.5 `Delete` (2)

**`Delete_ShouldSoftDeleteProductAndPhotos`**
- `ProductRepo.DeleteAsync` returns `true`.
- `PhotoRepo.GetAllWithDeletedAsync(...)` returns one photo
  (Id=10, `imgs/product/a.png`, ProductId=1).
- Asserts OK, `SoftDeleteImageAsync(10, "imgs/product/a.png")` called once,
  `Commit()` called once.

> Exercises soft-delete product + cascade soft-delete of its photos.

**`Delete_WhenProductMissing_ShouldReturnNotFound`**
- `DeleteAsync` returns `false` → 404.

### 4.6 `ApplyDiscount` (2)

**`ApplyDiscount_WhenNoProductIds_ShouldReturnBadRequest`**
- Form has empty `ProductIds` list → `BadRequestObjectResult` + 400.

**`ApplyDiscount_HappyPath_ShouldApplyToProducts`**
- ProductIds = [1,2]; discount repo returns `TblDiscount { Id = 5 }`.
- Asserts OK, `AddDiscountAsync(discount)` once, and
  `AddDiscountToProductsAsync(discount, ids)` where `ids` contains **both** 1 and 2.

### 4.7 `ApplyCategoryDiscount` (3)

**`ApplyCategoryDiscount_WhenNoCategoryId_ShouldReturnBadRequest`**
- `CategoryId = null` → 400 BadRequest.

**`ApplyCategoryDiscount_WhenNoProductsInCategory_ShouldReturnNotFound`**
- `ProductRepo.GetAllWithDeletedAsync(...)` returns empty list → 404.

**`ApplyCategoryDiscount_HappyPath_ShouldExpandToAllCategoryProducts`**
- Repo returns 2 products (Ids 1,2) in category 3.
- Asserts OK/200 and that `AddDiscountToProductsAsync` was called with exactly
  2 ids: `ids.Count() == 2 && ids.Contains(1) && ids.Contains(2)`.

> Proves "category discount" is expanded into per-product discount links.

---

## 5. `CategoryControllerTests.cs` (11 tests)

Covers route `api/category` — `CategoryController` in
`Src\ECom.Api\Controllers\CategoryController.cs`.

### Fixtures
- `SampleCategory()` → `TblCategories` Id=1, "Electronics", RowVersion [1,2],
  one `TblPhoto` (Id=10, `imgs/category/a.png`).
- `SampleDto()` → matching `CategoryDto` with empty Photos.

### Test summary

| Test | What it verifies |
|---|---|
| `GetAll_ShouldReturnOkWithMappedCategories` | OK + 200 + same dto list in `Data` |
| `GetById_WhenCategoryExists_ShouldReturnOk` | OK + 200 + Id=1 mapped |
| `GetById_WhenCategoryMissing_ShouldReturnNotFound` | `GetByIdAsync` null → 404 |
| `Add_ShouldAddCategorySaveImagesAndCommit` | save images ("category"), `AddAsync` once, `Commit` once |
| `Update_ShouldUpdateFieldsAttachRowVersionAndCommit` | Name/Description updated, `UpdatedAt` set, RowVersion replaced with client `[7,6]`, `HandleUpdatedImgs(..., null, categoryId, "1")`, commit once |
| `Update_WhenCategoryMissing_ShouldReturnNotFound` | null → 404 |
| `Update_WhenConcurrencyConflict_ShouldReturn409` | `Commit` throws concurrency → `ConflictObjectResult` 409 |
| `Delete_ShouldSoftDeleteCategoryAndPhotos` | `SoftDeleteImageAsync(10, ...)` + commit |
| `Delete_WhenCategoryMissing_ShouldReturnNotFound` | `DeleteAsync` false → 404 |

**Update detail worth studying:** the controller explicitly sets
`category.UpdatedAt = DateTime.UtcNow;` — the test asserts `Assert.NotNull(category.UpdatedAt)`,
then that the client `RowVersion` `[7,6]` replaced the entity's `[1,2]`.

---

## 6. `FormDtoExtensionsTests.cs` (9 tests)

Pure unit tests (no mocks) of the **`ToDto()`** extension methods in
`Src\ECom.Api\DTO\*.cs`. These convert HTTP form DTOs (`*FormDto`) into Core DTOs:

### 6.1 `AddProductFormDto.ToDto()` (3)

| Test | Behavior verified |
|---|---|
| `AddProductForm_WithDiscountPosted_ShouldPopulateDiscountDto` | Discount is built only when `DiscountFrom`/`To`/`Type` all have values; asserts From, To, Type=Percentage, Value=15, Name copied |
| `AddProductForm_WithoutDiscountInfo_ShouldLeaveDiscountNull` | If discount fields absent → `dto.Discount == null` |
| `AddProductForm_WithFiles_ShouldConvertToFileParameters` | A real `IFormFile` is converted via `FileParameterConverter` into `FileParameterDto` with same bytes, FileName, ContentType |

> The discount condition lives in `AddProductFormExtensions.ToDto`:
> ```csharp
> if (form.DiscountFrom.HasValue && form.DiscountTo.HasValue && form.DiscountType.HasValue)
> { dto.Discount = new AddDiscountDto { ... Value = form.DiscountValue ?? 0, ... }; }
> ```

### 6.2 `UpdateProductFormDto.ToDto()` (3)

| Test | Behavior verified |
|---|---|
| `UpdateProductForm_WithRowVersion_ShouldConvertFromBase64` | Base64 string → plain `byte[]` via `Convert.FromBase64String` |
| `[Theory] UpdateProductForm_WithoutRowVersion_ShouldLeaveItNull` | null **and** "" both yield `dto.RowVersion == null` (guards `string.IsNullOrEmpty`) |
| (rows 102-120) | conclusion inline |

### 6.3 `UpdateCategoryFormDto.ToDto()` (2)

| Test | Behavior verified |
|---|---|
| `UpdateCategoryForm_WithRowVersion_ShouldConvertFromBase64` | `[7,8]` round-trips |
| `[Theory] UpdateCategoryForm_WithoutRowVersion_ShouldLeaveItNull` | null/"" → null |

### 6.4 `AddCategoryFormDto.ToDto()` (2)

| Test | Behavior verified |
|---|---|
| `AddCategoryForm_ShouldMapScalarFields` | Name/Description copied; `Files` non-null and empty |
| `AddCategoryForm_WithFiles_ShouldConvertToFileParameters` | file conversion (bytes, name `cat.png`, content type) |

> **Study note — `[Theory]` + `[InlineData]`:** one test method body runs with each
> inline data row, so a single test covers both null and empty string.

---

## 7. `MappingConfigurationTests.cs` (5 tests)

Boots **real AutoMapper** from the API assembly profiles (`ProductMapping`,
`CategoryMapping`) using `ServiceCollection` + `AddAutoMapper`. No mocks.

### 7.1 RowVersion is carried into DTOs

**`ProductToProductDto_ShouldCarryRowVersion`**
- entity RowVersion `[4,5]` → `ProductDto.RowVersion` equals `[4,5]`.
- Backed by `ProductMapping.CreateMap<TblProducts, ProductDto>().ForMember(dest.RowVersion, MapFrom(src => src.RowVersion))`.

**`CategoryToCategoryDto_ShouldCarryRowVersion`**
- `[2,2]` → `CategoryDto.RowVersion` `[2,2]`.

### 7.2 Update mapping overwrite / preserve semantics

**`UpdateProductDto_WithRowVersion_ShouldOverwriteEntityRowVersion`**
- `Map(dto, existingEntity)` with dto RowVersion `[9]` → entity becomes `[9]`.

**`UpdateProductDto_WithNullRowVersion_ShouldPreserveEntityRowVersion`**
- dto RowVersion `null` → entity keeps `[7]`.
- Backed by `ForMember(dest => dest.RowVersion, opt => opt.Condition(src => src.RowVersion != null))`.

**`UpdateProductDto_ToNewProduct_ShouldMapScalarsAndRowVersion`**
- `Map<TblProducts>(dto)` creates a fresh entity with Name/Price/CategoryId and
  RowVersion `[3,3,3]`.

> **Study note:** these tests pin the **concurrency semantics**: a null client token
> must NOT wipe the server token. This is why the controller also has defensive
> `if (productDto.RowVersion != null)` checks.

---

## 8. `ExceptionHandlingMiddlewareTests.cs` (3 tests)

Tests `ExceptionHandlingMiddleware` (`Src\ECom.Api\Middleware\...`) using a raw
`DefaultHttpContext` and a response `MemoryStream`. The middleware only intercepts
**`DbUpdateConcurrencyException`**; everything else propagates.

### Helper
```csharp
CreateMiddleware(next) =>
    new ExceptionHandlingMiddleware(ctx => next(ctx), NullLogger<...>.Instance)
```
`NullLogger` avoids real logging in tests.

### Tests

**`WhenConcurrencyExceptionThrown_ShouldReturn409WithJsonResponse`**
- Next delegate throws `DbUpdateConcurrencyException`.
- After `Invoke`:
  - `context.Response.StatusCode == 409`
  - `ContentType` starts with `"application/json"`
  - body deserializes to a `responseApi` with StatusCode 409 and StatusMessage
    `"The record was modified by another user. Please reload and try again."`
- The body is read back with `Body.Seek(0)` + `StreamReader.ReadToEndAsync` + `JsonSerializer`.

**`WhenNoExceptionThrown_ShouldCallNextAndLeaveResponseUntouched`**
- Next sets a flag + completes; existing 200 status is preserved.
- Asserts `nextCalled == true` and status still 200.

**`WhenOtherExceptionThrown_ShouldPropagate`**
- Next throws `InvalidOperationException` → `Assert.ThrowsAsync<InvalidOperationException>`.
- Proves non-concurrency exceptions are **not** swallowed.

> **Study note:** the middleware picks up concurrency conflicts that happen
> *outside* the controllers (e.g., in background jobs or non-caught paths),
> guaranteeing a consistent 409 JSON `responseApi` everywhere.

---

## 9. `Services/ImageManagementServiceTests.cs` (13 tests)

Tests the real `ImageManagementService`
(`Src\ECom.Infrastructure\Repositories\Services\ImageManagementService.cs`).

### Setup (important to understand)
- **Static ctor:** `GlobalConfiguration.Configuration.UseMemoryStorage();`
  → Hangfire jobs can be scheduled without a real server/DB.
- **Instance ctor:** creates a real **temp folder** as `WebRootPath`
  (`Path.Combine(Path.GetTempPath(), "webroot-test-<guid>")`).
- `Dispose()` deletes that temp folder — tests are self-cleaning.
- `IPhotoRepo` is mocked; `IWebHostEnvironment.WebRootPath` returns the temp folder.

### 9.1 `AddImagesAsync` (5)

| Test | Behavior verified |
|---|---|
| `AddImagesAsync_NullFiles_ShouldReturnEmptyList` | null input → empty list, no files written |
| `AddImagesAsync_EmptyFiles_ShouldReturnEmptyList` | empty list → empty result |
| `AddImagesAsync_FileWithNoData_ShouldBeSkipped` | `Data` empty byte[] → skipped entirely |
| `AddImagesAsync_HappyPath_ShouldWriteFilesAndReturnRelativePaths` | 2 files written to `wwwroot/imgs/product/`; returned paths start with `"imgs/product/"`; actual files exist on disk (then cleaned up) |
| `AddImagesAsync_FileWithoutExtension_ShouldBeSavedWithoutExtension` | filename with no extension → path is `imgs/category/` + a 32-char GUID (no extension, `Guid.NewGuid().ToString("N")` is exactly 32 chars) |

> Note the physical-files truth check: `File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path...))`.
> `CleanupAddImages` removes the test-created `wwwroot` folders afterwards.

### 9.2 `SoftDeleteImageAsync` (1)

**`SoftDeleteImageAsync_ShouldSoftDeleteRecordAndSchedulePurge`**
- `DeleteAsync` returns true.
- Asserts `PhotoRepo.DeleteAsync([42])` called once (the id arg is wrapped in an
  `object[]`).
- Also schedules a Hangfire job to purge the file after 3 days
  (`BackgroundJob.Schedule<IImageManagementService>(svc => svc.PurgeImageFileAsync(id, src), TimeSpan.FromDays(3))`).

### 9.3 `PurgeImageFileAsync` (4)

The method: delete physical file (if exists) + `HardDeleteImage(id)`.

| Test | Behavior verified |
|---|---|
| `PurgeImageFileAsync_ShouldDeletePhysicalFileAndHardDeleteRecord` | creates a real file under the temp webroot; after call the file is gone **and** `HardDeleteImage(7)` called |
| `PurgeImageFileAsync_WhenFileMissing_ShouldStillHardDeleteRecord` | missing file must not throw; hard delete still happens |
| `PurgeImageFileAsync_WithLeadingSlash_ShouldResolveInsideWebRoot` | `/imgs/...` resolves to inside webroot (`TrimStart('/', '\\')`) |
| `[Theory] PurgeImageFileAsync_WhenPathIsNullOrEmpty_ShouldNotThrow` | null/"" → no throw, hard delete still happens |

> `DeleteFileIfExists` also guards against path traversal via `Path.GetFullPath`.

### 9.4 `PurgeOldDeletedImagesAsync` (4) + helper

Helper `SetupGetAllWithDeleted(repo, result)` mocks
`GetAllWithDeletedAsync(anyPredicate, trackChanges, includes)`.

| Test | Behavior verified |
|---|---|
| `PurgeOldDeletedImagesAsync_WhenNoSoftDeletedPhotos_ShouldNotHardDeleteAnything` | empty list → `HardDeleteImage` never called |
| `PurgeOldDeletedImagesAsync_WhenNullList_ShouldNotHardDeleteAnything` | null list → never called (null-guard) |
| `PurgeOldDeletedImagesAsync_ShouldQueryOnlyDeletedPhotosWithoutTracking` | verifies `GetAllWithDeletedAsync(..., trackChanges: false, ...)` — i.e. predicate = IsDeleted==true, no tracking |
| `PurgeOldDeletedImagesAsync_HappyPath_ShouldDeleteFilesAndHardDeleteAllRecords` | 4 photos (existing file, missing file, empty path, null path): file deleted for the existing one; `HardDeleteImage(1..4)` each called once — all handled without exception |

> Together these guarantee the batch purge is **null-safe**, tolerates bad paths,
> and always cleans up DB records regardless of file state.

---

## 10. `Repositories/RepositoryTests.cs` (1 test)

Verifies the real **EF Core repository against an InMemory database**
(`AppDbContext` + `UseInMemoryDatabase`, unique DB name per call
`"db-<guid>"` so tests don't collide).

**`CategoryRepo_AddAsync_AddsAndPersists`**
- AAA-style layout with comments.
- `Arrange:` fresh `AppDbContext` + real `CategoryRepo`.
- `Act:` `repo.AddAsync(cat)` then `db.SaveChangesAsync()`.
- `Assert:` added entity not null, `Id > 0` (DB generated key), and
  `GetByIdAsync(added.Id)` returns the row (proves persistence).

> **Study note:** this is the only test using a real EF `DbContext`; it validates the
> repository actually talks to EF and that the entity is configured with a
> generated identity key. `using var db` disposes the context at method end.

---

## 11. Production code map (what the tests lock in)

| Subject | Location | Behavior tested |
|---|---|---|
| Product CRUD + discounts | `Controllers/ProductController.cs` | returns, validation branches, commit/rowversion, category-name enrichment |
| Category CRUD | `Controllers/CategoryController.cs` | same, plus `UpdatedAt` |
| Form→DTO conversion | `DTO/AddProductFormDto.cs`, `UpdateProductFormDto.cs`, `AddCategoryFormDto.cs`, `UpdateCategoryFormDto.cs` | discount building, base64 rowversion, file conversion |
| File conversion | `Helper/FileParameterConverter.cs` | `IFormFile` → bytes/name/contenttype |
| API envelope | `Helper/responseApi.cs` | always present in controller responses |
| AutoMapper profiles | `Mapping/ProductMapping.cs`, `Mapping/CategoryMapping.cs` | RowVersion conditional mapping, photo url lists, category name |
| Middleware | `Middleware/ExceptionHandlingMiddleware.cs` | concurrency → 409 JSON, others propagate |
| Image service | `Infrastructure/Repositories/Services/ImageManagementService.cs` | add/soft-delete/purge/batch purge |
| Base controller | `Controllers/MyBaseController.cs` | just holds `IUnitOfWork` + `IMapper` |

---

## 12. Recurring study patterns (cheat-sheet)

1. **`[Fact]`** = a single test; **`[Theory]` + `[InlineData]`** = the same body over several inputs.
2. **Arrange** (build mocks/data) → **Act** (call controller/service) → **Assert** (check HTTP result, envelope, calls).
3. **HTTP result assertions cascade:**
   `Assert.IsType<OkObjectResult>` → `.Value as responseApi` → `StatusCode` → `Data`.
4. **`Assert.Same`** proves pass-through of the exact instance (mapper output identity).
5. **`Verify(..., Times.Once/ Never)`** proves *behavior* (that a dependency was actually used).
6. **`It.IsAny<T>()`** = "any value"; **`It.Is<T>(pred)`** = "a value matching a predicate" (used to inspect real args).
7. **RowVersion** semantics: client token always overwrites the entity unless null; AutoMapper `Condition` + controller `if != null` guarantee it.
8. **Filesystem tests** create/delete real temp files and clean up in `finally` / `Dispose`.
9. **Hangfire** is made test-friendly via `MemoryStorage` (no real server needed).
10. **`using var`** disposes `DbContext`s; xUnit creates a fresh test-class instance per test (isolated state).

---

## 13. Potential growth areas (what is NOT yet covered)

- Error branch of `ApplyCategoryDiscount` when `AddImagesAsync` fails / IO exception.
- Validation `[ApiController]` auto-400 on model-binding failures (e.g., missing Name).
- Multi-thread / race in Hangfire purge scheduling.
- `GetAllWithDeletedAsync` with includes in repository test (only add path tested).
- Tests for `PhotoRepo.HandleUpdatedImgs` itself (currently mocked everywhere).
- `PurgeOldDeletedImagesAsync` verifying the Hangfire storage purge job (only method-level behavior).