using AutoMapper;

using Ecom.Core.DTO.Categories;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

namespace ECom.Api.Tests.Mapping
{
    public class ProfileConfigurationTests
    {
        [Fact]
        public void AllProfiles_ShouldBeValidlyConfigured()
        {
            var mapper = MappingTestHelper.CreateMapper();

            mapper.ConfigurationProvider.AssertConfigurationIsValid();
        }

        [Fact]
        public void Profiles_ShouldActuallyBeLoaded()
        {
            var mapper = MappingTestHelper.CreateMapper();

            var productDto = mapper.Map<ProductDto>(new TblProducts
            {
                Id = 1,
                Name = "Laptop",
                Category = new TblCategories { Id = 3, Name = "Electronics" }
            });

            var categoryDto = mapper.Map<CategoryDto>(new TblCategories { Id = 1, Name = "Electronics" });

            // AssertConfigurationIsValid would pass vacuously if no profile was discovered,
            // so assert on behaviour that only a configured profile can produce.
            Assert.Equal("Electronics", productDto.CategoryName);
            Assert.Equal(1, categoryDto.Id);
        }
    }
}
