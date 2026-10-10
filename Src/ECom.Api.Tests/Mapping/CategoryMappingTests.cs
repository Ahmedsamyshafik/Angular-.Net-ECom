using AutoMapper;

using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;

namespace ECom.Api.Tests.Mapping
{
    public class CategoryMappingTests
    {
        private readonly IMapper _mapper;

        public CategoryMappingTests()
        {
            _mapper = MappingTestHelper.CreateMapper();
        }

        #region DTO -> Entity

        [Fact]
        public void AddCategoryDto_ToEntity_ShouldMapScalarsOnly()
        {
            var dto = new AddCategoryDto { Name = "Electronics", Description = "Devices" };

            var entity = _mapper.Map<TblCategories>(dto);

            Assert.Equal("Electronics", entity.Name);
            Assert.Equal("Devices", entity.Description);
            Assert.Equal(0, entity.Id);
            Assert.Empty(entity.Photos!);
            Assert.Empty(entity.Products!);
            Assert.Null(entity.ProductIds);
        }

        [Fact]
        public void UpdateCategoryDto_WithRowVersion_ShouldOverwriteEntityRowVersion()
        {
            var entity = new TblCategories { Id = 1, Name = "Old", RowVersion = new byte[] { 1 } };

            _mapper.Map(new UpdateCategoryDto
            {
                Id = 1,
                Name = "New Electronics",
                Description = "Updated",
                RowVersion = new byte[] { 2, 2 }
            }, entity);

            Assert.Equal("New Electronics", entity.Name);
            Assert.Equal("Updated", entity.Description);
            Assert.Equal(new byte[] { 2, 2 }, entity.RowVersion);
        }

        [Fact]
        public void UpdateCategoryDto_WithNullRowVersion_ShouldPreserveEntityRowVersion()
        {
            var entity = new TblCategories { RowVersion = new byte[] { 7 } };

            _mapper.Map(new UpdateCategoryDto { Id = 1, Name = "New Electronics", RowVersion = null }, entity);

            Assert.Equal(new byte[] { 7 }, entity.RowVersion);
        }

        [Fact]
        public void UpdateCategoryDto_OverExistingEntity_ShouldNotResetIdOrUpdatedAt()
        {
            var entity = new TblCategories { Id = 5, UpdatedAt = new DateTime(2021, 5, 5) };

            _mapper.Map(new UpdateCategoryDto { Id = 0, Name = "New Electronics" }, entity);

            Assert.Equal(5, entity.Id);
            Assert.Equal(new DateTime(2021, 5, 5), entity.UpdatedAt);
        }

        #endregion

        #region Entity -> DTO

        [Fact]
        public void EntityToCategoryDto_ShouldCarryRowVersion()
        {
            var entity = new TblCategories
            {
                Id = 2,
                Name = "Electronics",
                Description = "Devices",
                RowVersion = new byte[] { 2, 2 }
            };

            var dto = _mapper.Map<CategoryDto>(entity);

            Assert.Equal(new byte[] { 2, 2 }, dto.RowVersion);
            Assert.Equal(2, dto.Id);
            Assert.Equal("Electronics", dto.Name);
            Assert.Equal("Devices", dto.Description);
        }

        [Fact]
        public void EntityToCategoryDto_ShouldExcludeSoftDeletedPhotos()
        {
            var entity = new TblCategories
            {
                Id = 1,
                Name = "Electronics",
                Photos = new List<TblPhoto>
                {
                    new TblPhoto { Id = 10, PhotoPath = "imgs/category/a.png" },
                    new TblPhoto { Id = 11, PhotoPath = "imgs/category/b.png", IsDeleted = true }
                }
            };

            var dto = _mapper.Map<CategoryDto>(entity);

            var photo = Assert.Single(dto.Photos);
            Assert.Equal(10, photo.Id);
            Assert.Equal("imgs/category/a.png", photo.Url);
        }

        [Fact]
        public void EntityToCategoryDto_WithNullPhotos_ShouldReturnEmptyList()
        {
            var entity = new TblCategories { Id = 1, Name = "Electronics", Photos = null };

            var dto = _mapper.Map<CategoryDto>(entity);

            Assert.NotNull(dto.Photos);
            Assert.Empty(dto.Photos);
        }

        #endregion
    }
}
