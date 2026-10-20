using AutoMapper;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;

namespace ECom.Api.Tests.Mapping
{
    public class OrderMappingTests
    {
        private readonly IMapper _mapper;

        public OrderMappingTests()
        {
            _mapper = MappingTestHelper.CreateMapper();
        }

        [Fact]
        public void EntityToCreateOrderResponse_ShouldProjectTheFrontendIOrderContract()
        {
            var order = new TblOrders(
                "buyer@example.com",
                30m,
                new TblShippingAddress("First", "Last", "City", "12345", "Street", "State"),
                new TblDeliveryMethod("Standard", "description", 10m, "3-5 days"),
                new List<TblOrderItem>
                {
                    new(42, "imgs/product/a.png", "Widget", 10m, 3)
                })
            {
                Id = 7,
                CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
            };

            var dto = _mapper.Map<CreateOrderResponse>(order);

            Assert.Equal(7, dto.Id);
            Assert.Equal("buyer@example.com", dto.BuyerEmail);
            Assert.Equal(30m, dto.SubTotal);
            Assert.Equal(40m, dto.Total); // SubTotal + delivery method price
            Assert.Equal(order.CreatedAt, dto.OrderDate);
            Assert.Equal("Standard", dto.DeliveryMethod);
            Assert.Equal("Pending", dto.Status);

            Assert.Equal("First", dto.ShippingAddress.FirstName);
            Assert.Equal("Last", dto.ShippingAddress.LastName);
            Assert.Equal("City", dto.ShippingAddress.City);
            Assert.Equal("12345", dto.ShippingAddress.ZipCode);
            Assert.Equal("Street", dto.ShippingAddress.Street);
            Assert.Equal("State", dto.ShippingAddress.State);

            var item = Assert.Single(dto.OrderItems);
            Assert.Equal(42, item.ProductItemId);
            Assert.Equal("imgs/product/a.png", item.MainImage);
            Assert.Equal("Widget", item.ProductName);
            Assert.Equal(10m, item.Price);
            Assert.Equal(3, item.Quntity);
        }
    }
}
