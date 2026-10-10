using AutoMapper;

using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

namespace ECom.Api.Tests.Mapping
{
    public class ProductMappingTests
    {
        private readonly IMapper _mapper;

        public ProductMappingTests()
        {
            _mapper = MappingTestHelper.CreateMapper();
        }

        #region DTO -> Entity

        [Fact]
        public void AddProductDto_ToEntity_ShouldMapScalarsOnly()
        {
            var dto = new AddProductDto
            {
                Id = 99,
                Name = "Laptop",
                Description = "A laptop",
                Price = 1200m,
                CategoryId = 3
            };

            var entity = _mapper.Map<TblProducts>(dto);

            Assert.Equal("Laptop", entity.Name);
            Assert.Equal("A laptop", entity.Description);
            Assert.Equal(1200m, entity.Price);
            Assert.Equal(3, entity.CategoryId);
            Assert.Equal(0, entity.Id);
        }

        [Fact]
        public void AddProductDto_ToEntity_ShouldNotTouchNavigationOrLifecycleMembers()
        {
            var dto = new AddProductDto { Name = "Laptop", Description = "A laptop", Price = 1m, CategoryId = 1 };

            var entity = _mapper.Map<TblProducts>(dto);

            Assert.Null(entity.Category);
            Assert.Empty(entity.Photos!);
            Assert.Empty(entity.ProductDiscounts);
            Assert.False(entity.IsDeleted);
        }

        [Fact]
        public void UpdateProductDto_OverExistingEntity_ShouldMapScalars()
        {
            var entity = new TblProducts { Id = 1, Name = "Old" };

            _mapper.Map(new UpdateProductDto
            {
                Id = 1,
                Name = "Laptop",
                Description = "A laptop",
                Price = 10m,
                CategoryId = 1
            }, entity);

            Assert.Equal("Laptop", entity.Name);
            Assert.Equal("A laptop", entity.Description);
            Assert.Equal(10m, entity.Price);
            Assert.Equal(1, entity.CategoryId);
        }

        [Fact]
        public void UpdateProductDto_WithRowVersion_ShouldOverwriteEntityRowVersion()
        {
            var entity = new TblProducts { RowVersion = new byte[] { 1 } };

            _mapper.Map(new UpdateProductDto { Name = "Laptop", RowVersion = new byte[] { 9 } }, entity);

            Assert.Equal(new byte[] { 9 }, entity.RowVersion);
        }

        [Fact]
        public void UpdateProductDto_WithNullRowVersion_ShouldPreserveEntityRowVersion()
        {
            var entity = new TblProducts { RowVersion = new byte[] { 7 } };

            _mapper.Map(new UpdateProductDto { Name = "Laptop", RowVersion = null }, entity);

            Assert.Equal(new byte[] { 7 }, entity.RowVersion);
        }

        [Fact]
        public void UpdateProductDto_OverExistingEntity_ShouldNotResetIdOrLifecycleMembers()
        {
            var entity = new TblProducts
            {
                Id = 5,
                IsDeleted = true,
                CreatedAt = new DateTime(2020, 1, 1),
                DeletedAt = new DateTime(2021, 1, 1)
            };

            _mapper.Map(new UpdateProductDto { Id = 0, Name = "Laptop" }, entity);

            Assert.Equal(5, entity.Id);
            Assert.True(entity.IsDeleted);
            Assert.Equal(new DateTime(2020, 1, 1), entity.CreatedAt);
            Assert.Equal(new DateTime(2021, 1, 1), entity.DeletedAt);
        }

        #endregion

        #region Entity -> DTO

        [Fact]
        public void EntityToProductDto_ShouldCarryRowVersion()
        {
            var entity = new TblProducts { Id = 1, Name = "Laptop", Price = 10m, RowVersion = new byte[] { 4, 5 } };

            var dto = _mapper.Map<ProductDto>(entity);

            Assert.Equal(new byte[] { 4, 5 }, dto.RowVersion);
            Assert.Equal(1, dto.Id);
            Assert.Equal("Laptop", dto.Name);
            Assert.Equal(10m, dto.RealPrice);
        }

        [Fact]
        public void EntityToProductDto_ShouldMapPriceOntoRealPriceAndLeavePriceAfterDiscountToTheCalculator()
        {
            var entity = new TblProducts { Id = 1, Name = "Laptop", Price = 1000m };

            var dto = _mapper.Map<ProductDto>(entity);

            Assert.Equal(1000m, dto.RealPrice);

            // The profile has no access to the product's discounts, so it must never populate this.
            // Same convention as DiscountMapping ignoring TblDiscount.IsActive.
            Assert.Null(dto.PriceAfterDiscount);
        }

        [Fact]
        public void EntityToProductDto_ShouldTakeCategoryNameFromNavigation()
        {
            var entity = new TblProducts
            {
                Id = 1,
                Name = "Laptop",
                Category = new TblCategories { Id = 3, Name = "Electronics" }
            };

            var dto = _mapper.Map<ProductDto>(entity);

            Assert.Equal("Electronics", dto.CategoryName);
        }

        [Fact]
        public void EntityToProductDto_WithNullCategory_ShouldLeaveCategoryNameNull()
        {
            var entity = new TblProducts { Id = 1, Name = "Laptop", Category = null };

            var dto = _mapper.Map<ProductDto>(entity);

            Assert.Null(dto.CategoryName);
        }

        [Fact]
        public void EntityToProductDto_ShouldExcludeSoftDeletedPhotos()
        {
            var entity = new TblProducts
            {
                Id = 1,
                Name = "Laptop",
                Photos = new List<TblPhoto>
                {
                    new TblPhoto { Id = 10, PhotoPath = "imgs/product/a.png" },
                    new TblPhoto { Id = 11, PhotoPath = "imgs/product/b.png", IsDeleted = true }
                }
            };

            var dto = _mapper.Map<ProductDto>(entity);

            var photo = Assert.Single(dto.PhotoUrls);
            Assert.Equal(10, photo.Id);
            Assert.Equal("imgs/product/a.png", photo.Url);
        }

        [Fact]
        public void EntityToProductDto_WithNullPhotos_ShouldReturnEmptyList()
        {
            var entity = new TblProducts { Id = 1, Name = "Laptop", Photos = null };

            var dto = _mapper.Map<ProductDto>(entity);

            Assert.NotNull(dto.PhotoUrls);
            Assert.Empty(dto.PhotoUrls);
        }

        #endregion
    }
}
