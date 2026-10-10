using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ECom.Api.Tests.Repositories
{
    public class CategoryQueryTests
    {
        private static AppDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("db-" + Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(options);
        }

        private static DateTime BaseDate() => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static TblCategories Make(int id, string name, string description, DateTime? createdAt = null)
        {
            return new TblCategories
            {
                Id = id,
                Name = name,
                Description = description,
                CreatedAt = createdAt ?? BaseDate().AddDays(id)
            };
        }

        private static TblProducts MakeProduct(int id, int categoryId, bool isDeleted = false)
        {
            return new TblProducts
            {
                Id = id,
                Name = $"Product {id}",
                Description = $"Description {id}",
                Price = id * 1.00m,
                CategoryId = categoryId,
                IsDeleted = isDeleted
            };
        }

        // A fresh store containing only the given categories/products. Any framework/seed rows
        // (Ids <= 100) are cleared first so assertions stay deterministic.
        private static async Task<AppDbContext> SeedAsync(
            IEnumerable<TblCategories>? categories = null,
            IEnumerable<TblProducts>? products = null)
        {
            var db = NewContext();

            db.TblProducts.RemoveRange(db.TblProducts.Where(p => p.Id <= 100).ToList());
            db.TblCategories.RemoveRange(db.TblCategories.Where(c => c.Id <= 100).ToList());
            await db.SaveChangesAsync();

            if (categories != null)
            {
                db.TblCategories.AddRange(categories);
            }
            if (products != null)
            {
                db.TblProducts.AddRange(products);
            }
            await db.SaveChangesAsync();

            return db;
        }

        private static List<TblCategories> PageData()
        {
            var list = new List<TblCategories>();
            for (int i = 0; i < 23; i++)
            {
                int id = 101 + i;
                list.Add(Make(id, $"Category {id:D3}", $"Description for category {id}"));
            }
            return list;
        }

        private static TblCategories[] SearchData() => new[]
        {
            Make(401, "Electronics", "devices and gadgets"),
            Make(402, "Gaming", "consoles and accessories"),
            Make(403, "Books Store", "literature and novels")
        };

        private static TblCategories[] SortData() => new[]
        {
            Make(601, "Delta", "d", BaseDate().AddDays(5)),
            Make(602, "Alpha", "d", BaseDate().AddDays(1)),
            Make(603, "Charlie", "d", BaseDate().AddDays(4)),
            Make(604, "Bravo", "d", BaseDate().AddDays(2)),
            Make(605, "Echo", "d", BaseDate().AddDays(3))
        };

        [Fact]
        public async Task GetPageAsync_DefaultParams_ReturnsFirstPageSortedById()
        {
            using var db = await SeedAsync(PageData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams());

            Assert.Equal(1, page.PageNumber);
            Assert.Equal(10, page.PageSize);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(10, page.Data.Count);
            Assert.Equal(new[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SecondPage_ReturnsNextSlice()
        {
            using var db = await SeedAsync(PageData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { PageNumber = 2 });

            Assert.Equal(10, page.Data.Count);
            Assert.Equal(111, page.Data.First().Id);
            Assert.Equal(120, page.Data.Last().Id);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
        }

        [Fact]
        public async Task GetPageAsync_LastPartialPage_ReturnsRemainder()
        {
            using var db = await SeedAsync(PageData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { PageNumber = 3 });

            Assert.Equal(3, page.Data.Count);
            Assert.Equal(new[] { 121, 122, 123 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_BeyondLastPage_ReturnsEmptyPage()
        {
            using var db = await SeedAsync(PageData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { PageNumber = 10 });

            Assert.Empty(page.Data);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(10, page.PageNumber);
        }

        [Fact]
        public async Task GetPageAsync_SearchByName_MatchesName()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "Books" });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(403, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_SearchByDescription_MatchesDescription()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "novels" });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(403, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_SearchMultipleTerms_AreAnded()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "Books novels" });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(403, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_SearchWhitespace_ReturnsAll()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "   " });

            Assert.Equal(3, page.TotalCount);
        }

        [Fact]
        public async Task GetPageAsync_SearchNoMatch_ReturnsEmptyPage()
        {
            using var db = await SeedAsync(SearchData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "not-a-category" });

            Assert.Empty(page.Data);
            Assert.Equal(0, page.TotalCount);
            Assert.Equal(0, page.TotalPages);
        }

        [Fact]
        public async Task GetPageAsync_FilterHasProducts_True_OnlyCategoriesWithProducts()
        {
            using var db = await SeedAsync(
                categories: new[]
                {
                    Make(501, "Alpha", "d"),
                    Make(502, "Bravo", "d"),
                    Make(503, "Charlie", "d")
                },
                products: new[]
                {
                    MakeProduct(2001, 501),
                    MakeProduct(2003, 503, isDeleted: true)
                });
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { HasProducts = true });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(501, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_FilterHasProducts_False_OnlyCategoriesWithoutProducts()
        {
            using var db = await SeedAsync(
                categories: new[]
                {
                    Make(501, "Alpha", "d"),
                    Make(502, "Bravo", "d"),
                    Make(503, "Charlie", "d")
                },
                products: new[]
                {
                    MakeProduct(2001, 501),
                    MakeProduct(2003, 503, isDeleted: true)
                });
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { HasProducts = false });

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { 502, 503 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByIdDescending_OrdersNewestFirst()
        {
            using var db = await SeedAsync(SortData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { SortBy = "id", SortOrder = "desc" });

            Assert.Equal(new[] { 605, 604, 603, 602, 601 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByNameAscending_OrdersByName()
        {
            using var db = await SeedAsync(SortData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { SortBy = "name", SortOrder = "asc" });

            Assert.Equal(new[] { 602, 604, 603, 601, 605 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortByCreatedAtAscending_OrdersByCreatedAt()
        {
            using var db = await SeedAsync(SortData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { SortBy = "createdAt", SortOrder = "asc" });

            Assert.Equal(new[] { 602, 604, 605, 603, 601 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SortOrderCaseInsensitive_DegradesToAscendingWhenNotDesc()
        {
            using var db = await SeedAsync(SortData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { SortBy = "name", SortOrder = "up" });

            Assert.Equal(new[] { 602, 604, 603, 601, 605 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_InvalidSortField_FallsBackToIdAscending()
        {
            using var db = await SeedAsync(SortData());
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { SortBy = "banana" });

            Assert.Equal(new[] { 601, 602, 603, 604, 605 }, page.Data.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task GetPageAsync_SoftDeletedCategories_AreExcluded()
        {
            using var db = await SeedAsync(
                categories: new[] { Make(801, "Archived", "d"), Make(802, "Active", "d") });
            var loaded = db.TblCategories.Single(c => c.Id == 801);
            loaded.IsDeleted = true;
            await db.SaveChangesAsync();

            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams { PageSize = 10 });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(802, page.Data.Single().Id);
        }

        [Fact]
        public async Task GetPageAsync_Combined_FilterSearchSortAndPage()
        {
            using var db = await SeedAsync(
                categories: new[]
                {
                    Make(701, "Gaming Laptop", "high performance gpu", BaseDate().AddDays(5)),
                    Make(702, "Gaming Console", "home entertainment", BaseDate().AddDays(1)),
                    Make(703, "Office Laptop", "portable work", BaseDate().AddDays(3)),
                    Make(704, "Books", "novels", BaseDate().AddDays(2)),
                    Make(705, "Gaming Table", "desk", BaseDate().AddDays(4))
                },
                products: new[]
                {
                    MakeProduct(3001, 701),
                    MakeProduct(3002, 702),
                    MakeProduct(3003, 703),
                    MakeProduct(3004, 704)
                });
            var repo = new CategoryRepo(db);

            var page = await repo.GetPageAsync(new CategoryQueryParams
            {
                HasProducts = true,
                Search = "Gaming",
                SortBy = "createdAt",
                SortOrder = "desc",
                PageSize = 1,
                PageNumber = 2
            });

            // Search "gaming" matches 701, 702, 705 ; HasProducts=true removes 705 (no products).
            // Sorted createdAt desc: 701(+5), 702(+1) -> page 2 holds 702.
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(2, page.TotalPages);
            Assert.Single(page.Data);
            Assert.Equal(702, page.Data[0].Id);
        }

        [Fact]
        public async Task GetPageAsync_IncludesPhotosNavigation()
        {
            using var db = await SeedAsync(
                categories: new[] { Make(901, "Alpha", "d") });

            db.TblPhotos.Add(new TblPhoto { Id = 1, PhotoPath = "imgs/category/901a.png", CategoryId = 901 });
            db.TblPhotos.Add(new TblPhoto { Id = 2, PhotoPath = "imgs/category/901b.png", CategoryId = 901 });
            await db.SaveChangesAsync();

            var repo = new CategoryRepo(db);
            var page = await repo.GetPageAsync(new CategoryQueryParams { Search = "Alpha" });

            var loaded = Assert.Single(page.Data);
            Assert.Equal(2, loaded.Photos!.Count);
            Assert.Contains(loaded.Photos, ph => ph.PhotoPath == "imgs/category/901a.png");
        }
    }
}