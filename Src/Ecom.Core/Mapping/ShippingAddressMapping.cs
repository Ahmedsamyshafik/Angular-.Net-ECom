using AutoMapper;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;

namespace Ecom.Core.Mapping
{
    public class ShippingAddressMapping : Profile
    {
        public ShippingAddressMapping()
        {
            CreateMap<TblShippingAddress, shipAddressDto>().ReverseMap();
        }
    }
}
