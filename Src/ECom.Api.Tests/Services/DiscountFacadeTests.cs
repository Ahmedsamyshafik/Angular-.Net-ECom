using System;
using System.Linq;
using System.Threading.Tasks;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using ECom.Api.Tests.Mapping;
using ECom.Infrastructure.Application.Facades;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECom.Api.Tests.Services
{
    public class DiscountFacadeTests
    {
        private static AppDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("db-" + Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(options);
        }

        private static DiscountFacade CreateFacade(AppDbContext db)
        {
            return new DiscountFacade(
                new ProductRepo(db),
                new DiscountRepo(db),
                new DiscountProductRepo(db),
                MappingTestHelper.CreateMapper());
        }

        private static AddDiscountDto MakeDto(string name = "New Year")
        {
            return new AddDiscountDto
            {
                ProductIds = new List<int> { 1, 2 },
                From = DateTime.UtcNow.AddDays(-1),
                To = DateTime.UtcNow.AddDays(1),
                DiscountType = DiscountType.Percentage,
                Value = 10m,
                Name = name
            };
        }

        [Fact]
        public async Task ApplyDiscountAsync_WithoutProductIds_ShouldThrowBadRequest()
        {
            using var db = NewContext();
            var facade = CreateFacade(db);

            var dto = MakeDto();
            dto.ProductIds = new List<int>();

            await Assert.ThrowsAsync<BadRequestException>(() => facade.ApplyDiscountAsync(dto));
        }

        [Fact]
        public async Task ApplyDiscountAsync_ShouldMapPersistAndLinkProducts()
        {
            using var db = NewContext();
            var facade = CreateFacade(db);

            var dto = MakeDto();
            await facade.ApplyDiscountAsync(dto);

            Assert.Empty(db.TblDiscounts);
            Assert.Empty(db.TblProductDiscounts);

            await db.SaveChangesAsync();

            var discount = db.TblDiscounts.Single();
            Assert.Equal(DiscountType.Percentage, discount.Type);
            Assert.Equal(10m, discount.Value);
            Assert.Equal("New Year", discount.Name);
            Assert.Equal(dto.From, discount.StartDate);
            Assert.Equal(dto.To, discount.EndDate);
            Assert.True(discount.IsActive);

            Assert.Equal(2, db.TblProductDiscounts.Count(pd => pd.DiscountId == discount.Id));
        }

        [Fact]
        public async Task ApplyDiscountAsync_WhenDateRangeIsPast_ShouldMarkInactive()
        {
            using var db = NewContext();
            var facade = CreateFacade(db);

            var dto = MakeDto("Past");
            dto.From = DateTime.UtcNow.AddDays(-10);
            dto.To = DateTime.UtcNow.AddDays(-5);

            await facade.ApplyDiscountAsync(dto);

            Assert.Empty(db.TblDiscounts);

            await db.SaveChangesAsync();

            var discount = db.TblDiscounts.Single();
            Assert.False(discount.IsActive);
        }

        [Fact]
        public async Task ApplyCategoryDiscountAsync_WithoutCategoryId_ShouldThrowBadRequest()
        {
            using var db = NewContext();
            var facade = CreateFacade(db);

            var dto = MakeDto();
            dto.CategoryId = null;
            dto.ProductIds = new List<int>();

            await Assert.ThrowsAsync<BadRequestException>(() => facade.ApplyCategoryDiscountAsync(dto));
        }

        [Fact]
        public async Task ApplyCategoryDiscountAsync_WhenCategoryHasNoProducts_ShouldThrowNotFound()
        {
            using var db = NewContext();
            var facade = CreateFacade(db);

            var dto = MakeDto();
            dto.CategoryId = 77;
            dto.ProductIds = null;

            await Assert.ThrowsAsync<NotFoundException>(() => facade.ApplyCategoryDiscountAsync(dto));
        }

        [Fact]
        public async Task ApplyCategoryDiscountAsync_ShouldExpandToAllActiveCategoryProducts()
        {
            using var db = NewContext();
            db.TblProducts.AddRange(
                new TblProducts { Id = 1, Name = "A", Description = "x", Price = 1m, CategoryId = 3 },
                new TblProducts { Id = 2, Name = "B", Description = "y", Price = 2m, CategoryId = 3 },
                new TblProducts { Id = 3, Name = "C", Description = "z", Price = 3m, CategoryId = 3, IsDeleted = true });
            await db.SaveChangesAsync();

            var facade = CreateFacade(db);
            var dto = MakeDto();
            dto.CategoryId = 3;
            dto.ProductIds = null;

            await facade.ApplyCategoryDiscountAsync(dto);

            Assert.Equal(2, dto.ProductIds!.Count);
            Assert.Contains(1, dto.ProductIds);
            Assert.Contains(2, dto.ProductIds);

            Assert.Empty(db.TblDiscounts);
            Assert.Empty(db.TblProductDiscounts);

            await db.SaveChangesAsync();

            var discount = db.TblDiscounts.Single();
            Assert.Equal(2, db.TblProductDiscounts.Count(pd => pd.DiscountId == discount.Id));
        }
    }
}