using AutoMapper;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;

namespace Ecom.Core.Mapping
{
    public class OrderMapping : Profile
    {
        public OrderMapping()
        {
            // Entity -> read model of POST Order/CreateOrder (frontend IOrder).
            CreateMap<TblOrders, CreateOrderResponse>()
                .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.GetTotal()))
                .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.ShippingAddress, opt => opt.MapFrom(src => src.shippingAddress))
                .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.orderItems))
                .ForMember(dest => dest.DeliveryMethod, opt => opt.MapFrom(src => src.deliveryMethod.Name))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.status.ToString()));

            // Entity -> read model of an order line (frontend IOrderItem).
            CreateMap<TblOrderItem, OrderItemResponse>()
                .ForMember(dest => dest.MainImage, opt => opt.MapFrom(src => src.MainImg))
                .ForMember(dest => dest.Quntity, opt => opt.MapFrom(src => src.Quentity));
        }
    }
}
