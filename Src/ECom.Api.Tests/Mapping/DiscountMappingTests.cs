using AutoMapper;

using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;

namespace ECom.Api.Tests.Mapping
{
    public class DiscountMappingTests
    {
        private readonly IMapper _mapper;

        public DiscountMappingTests()
        {
            _mapper = MappingTestHelper.CreateMapper();
        }

        [Fact]
        public void AddDiscountDto_ToEntity_ShouldMapRenamedAndScalarMembers()
        {
            var from = new DateTime(2026, 1, 1);
            var to = new DateTime(2026, 2, 1);

            var entity = _mapper.Map<TblDiscount>(new AddDiscountDto
            {
                ProductIds = new List<int> { 1, 2 },
                From = from,
                To = to,
                DiscountType = DiscountType.Percentage,
                Value = 10m,
                Name = "New Year"
            });

            Assert.Equal("New Year", entity.Name);
            Assert.Equal(DiscountType.Percentage, entity.Type);
            Assert.Equal(10m, entity.Value);
            Assert.Equal(from, entity.StartDate);
            Assert.Equal(to, entity.EndDate);
        }

        [Fact]
        public void AddDiscountDto_WithNullName_ShouldFallBackToEmptyString()
        {
            var entity = _mapper.Map<TblDiscount>(new AddDiscountDto { Name = null, Value = 5m });

            Assert.Equal(string.Empty, entity.Name);
        }

        [Fact]
        public void AddDiscountDto_ToEntity_ShouldNotSetFacadeOwnedState()
        {
            var entity = _mapper.Map<TblDiscount>(new AddDiscountDto
            {
                Name = "New Year",
                From = new DateTime(2026, 1, 1),
                To = new DateTime(2026, 2, 1)
            });

            // IsActive is derived from the date window by the facade, never by the profile.
            Assert.False(entity.IsActive);
            Assert.Equal(0, entity.Id);
            Assert.Empty(entity.ProductDiscounts);
        }

        [Fact]
        public void AddDiscountDto_WithScopeSelectors_ShouldNotLeakThemIntoTheEntity()
        {
            var entity = _mapper.Map<TblDiscount>(new AddDiscountDto
            {
                CategoryId = 3,
                ProductIds = new List<int> { 1, 2 },
                Value = 7m
            });

            // CategoryId/ProductIds are the facade's targeting inputs, not entity state.
            Assert.Equal(7m, entity.Value);
            Assert.Empty(entity.ProductDiscounts);
            Assert.Equal(0, entity.Id);
        }
    }
}
