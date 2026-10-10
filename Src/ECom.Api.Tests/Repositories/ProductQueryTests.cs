using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ECom.Api.Tests.Repositories
{
    public class ProductQueryTests
    {
        private static AppDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("db-" + Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(options);
        }

        private static DateTime BaseDate() => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static TblProducts Make(int id, string name, string description, decimal price, int categoryId = 1, bool isDeleted = false, DateTime? createdAt = null)
        {
            return new TblProducts
            {
                Id = id,
                Name = name,
                Description = description,
                Price = price,
                CategoryId = categoryId,
                IsDeleted = isDeleted,
                CreatedAt = createdAt ?? BaseDate().AddDays(id)
            };
        }

        // A fresh store containing only the given products. Any framework/seed rows
        // (Ids <= 100) are cleared first so assertions stay deterministic.
        private static async Task<AppDbContext> SeedAsync(params TblProducts[] products)
        {
            var db = NewContext();

            db.TblProducts.RemoveRange(db.TblProducts.Where(p => p.Id <= 100).ToList());
            db.TblCategories.RemoveRange(db.TblCategories.Where(c => c.Id <= 100).ToList());
            await db.SaveChangesAsync();

            foreach (var categoryId in products.Select(p => p.CategoryId).Distinct())
            {
                db.TblCategories.Add(new TblCategories { Id = categoryId, Name = "Category " + categoryId, Description = "d" });
            }

            db.TblProducts.AddRange(products);
            await db.SaveChangesAsync();

            return db;
        }

        private static List<TblProducts> PageData()
        {
            var list = new List<TblProducts>();
            for (int i = 0; i < 23; i++)
            {
                int id = 101 + i;
                list.Add(Make(id, $"Product {id:D3}", $"Description for product {id}", id * 1.00m));
            }
            return list;
        }

        private static TblProducts[] SearchData() => new[]
        {
            Make(401, "Wireless Mouse", "optical sensor with programmable buttons", 10m),
            Make(402, "Wireless Keyboard", "mechanical keys", 20m),
            Make(403, "Gaming Pad", "large optical mouse surface", 30m),
            Make(404, "Wired Headset", "usb conference headset", 40m)
        };

        private static TblProducts[] SortData() => new[]
        {
            Make(501, "Delta", "d", 50m, createdAt: BaseDate().AddDays(5)),
            Make(502, "Alpha", "d", 10m, createdAt: BaseDate().AddDays(2)),
            Make(503, "Charlie", "d", 30m, createdAt: BaseDate().AddDays(4)),
            Make(504, "Bravo", "d", 20m, createdAt: BaseDate().AddDays(1)),
            Make(505, "Echo", "d", 40m, createdAt: BaseDate().AddDays(3))
        };

        [Fact]
        public async Task GetPageAsync_DefaultParams_ReturnsFirstPageSortedById()
        {
            using var db = await SeedAsync(PageData().ToArray());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams());

            Assert.Equal(1, page.PageNumber);
            Assert.Equal(10, page.PageSize);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(10, page.Data.Count);
            Assert.Equal(new[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SecondPage_ReturnsNextSlice()
        {
            using var db = await SeedAsync(PageData().ToArray());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { PageNumber = 2 });

            Assert.Equal(10, page.Data.Count);
            Assert.Equal(111, page.Data.First().Id);
            Assert.Equal(120, page.Data.Last().Id);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
        }

        [Fact]
        public async Task GetPageAsync_LastPartialPage_ReturnsRemainder()
        {
            using var db = await SeedAsync(PageData().ToArray());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { PageNumber = 3 });

            Assert.Equal(3, page.Data.Count);
            Assert.Equal(new[] { 121, 122, 123 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_BeyondLastPage_ReturnsEmptyPage()
        {
            using var db = await SeedAsync(PageData().ToArray());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { PageNumber = 10 });

            Assert.Empty(page.Data);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(10, page.PageSize);
            Assert.Equal(10, page.PageNumber);
        }

        [Fact]
        public async Task GetPageAsync_FilterByCategoryId_OnlyMatchesCategory()
        {
            using var db = await SeedAsync(
                Make(201, "Alpha", "d", 10m, categoryId: 7),
                Make(202, "Bravo", "d", 20m, categoryId: 8),
                Make(203, "Charlie", "d", 30m, categoryId: 7));
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { CategoryId = 7 });

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { 201, 203 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_FilterByPriceRange_OnlyMatchesRange()
        {
            using var db = await SeedAsync(
                Make(301, "Alpha", "d", 10m),
                Make(302, "Bravo", "d", 45m),
                Make(303, "Charlie", "d", 60m),
                Make(304, "Delta", "d", 90m));
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { MinPrice = 40m, MaxPrice = 70m });

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { 302, 303 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SearchByName_MatchesName()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { Search = "Wireless" });

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { 401, 402 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SearchByDescription_MatchesDescription()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { Search = "headset" });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(404, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_SearchMultipleTerms_AreAnded()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new ProductRepo(db);

            // Both terms must match the same product: "Wireless" -> 401,402 ; "Mouse" -> 401(name),403(description).
            var page = await repo.GetPageAsync(new ProductQueryParams { Search = "Wireless Mouse" });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(401, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_SearchWhitespace_ReturnsAll()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { Search = "   " });

            Assert.Equal(4, page.TotalCount);
        }

        [Fact]
        public async Task GetPageAsync_SearchNoMatch_ReturnsEmptyPage()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { Search = "does-not-exist" });

            Assert.Empty(page.Data);
            Assert.Equal(0, page.TotalCount);
            Assert.Equal(0, page.TotalPages);
        }

        [Fact]
        public async Task GetPageAsync_SortByIdDescending_OrdersNewestFirst()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "id", SortOrder = "desc" });

            Assert.Equal(new[] { 505, 504, 503, 502, 501 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByNameAscending_OrdersByName()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "name", SortOrder = "asc" });

            Assert.Equal(new[] { 502, 504, 503, 501, 505 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByPriceDescending_OrdersByPrice()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "price", SortOrder = "desc" });

            Assert.Equal(new[] { 501, 505, 503, 504, 502 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByCreatedAtAscending_OrdersByCreatedAt()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "createdAt", SortOrder = "asc" });

            Assert.Equal(new[] { 504, 502, 505, 503, 501 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortOrderCaseInsensitive_DegradesToAscendingWhenNotDesc()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "price", SortOrder = "up" });

            Assert.Equal(new[] { 502, 504, 503, 505, 501 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_InvalidSortField_FallsBackToIdAscending()
        {
            using var db = await SeedAsync(SortData());
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { SortBy = "banana" });

            Assert.Equal(new[] { 501, 502, 503, 504, 505 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SoftDeletedProducts_AreExcluded()
        {
            using var db = await SeedAsync(
                Make(601, "Alpha", "d", 10m),
                Make(602, "Bravo", "d", 20m, isDeleted: true),
                Make(603, "Charlie", "d", 30m));
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams { PageSize = 10 });

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { 601, 603 }, page.Data.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_Combined_FilterSearchSortAndPage()
        {
            using var db = await SeedAsync(
                Make(701, "Premium Laptop", "silver aluminum", 1500m, categoryId: 9),
                Make(702, "Budget Laptop", "entry level", 400m, categoryId: 9),
                Make(703, "Gaming Laptop", "rtx graphics", 2200m, categoryId: 9),
                Make(704, "Office Chair", "ergonomic", 300m, categoryId: 10),
                Make(705, "Pro Laptop Pro", "pro workstation", 1200m, categoryId: 9));
            var repo = new ProductRepo(db);

            var page = await repo.GetPageAsync(new ProductQueryParams
            {
                CategoryId = 9,
                MinPrice = 1000m,
                MaxPrice = 2500m,
                Search = "Laptop",
                SortBy = "price",
                SortOrder = "desc",
                PageSize = 2,
                PageNumber = 2
            });

            Assert.Equal(3, page.TotalCount);
            Assert.Equal(2, page.TotalPages);
            Assert.Single(page.Data);
            Assert.Equal(705, page.Data[0].Id);
        }

        [Fact]
        public async Task GetPageAsync_IncludesCategoryAndPhotos()
        {
            using var db = await SeedAsync();
            db.TblCategories.Add(new TblCategories { Id = 20, Name = "Category 20", Description = "d" });

            var product = Make(801, "Alpha", "d", 10m, categoryId: 20);
            product.Photos = new List<TblPhoto>
            {
                new TblPhoto { Id = 1, PhotoPath = "imgs/product/801a.png", ProductId = 801 },
                new TblPhoto { Id = 2, PhotoPath = "imgs/product/801b.png", ProductId = 801 }
            };
            db.TblProducts.Add(product);
            await db.SaveChangesAsync();

            var repo = new ProductRepo(db);
            var page = await repo.GetPageAsync(new ProductQueryParams { CategoryId = 20 });

            var loaded = Assert.Single(page.Data);
            Assert.Equal("Category 20", loaded.Category!.Name);
            Assert.Equal(2, loaded.Photos!.Count);
            Assert.Contains(loaded.Photos, ph => ph.PhotoPath == "imgs/product/801a.png");
        }
    }
}