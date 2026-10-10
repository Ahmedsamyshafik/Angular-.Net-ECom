using AutoMapper;

using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.Mapping
{
    public class ProductMapping : Profile
    {
        public ProductMapping()
        {
            CreateMap<AddProductDto, TblProducts>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.Photos, opt => opt.Ignore())
                .ForMember(dest => dest.ProductDiscounts, opt => opt.Ignore());

            CreateMap<UpdateProductDto, TblProducts>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.RowVersion, opt => opt.Condition(src => src.RowVersion != null))
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.Photos, opt => opt.Ignore())
                .ForMember(dest => dest.ProductDiscounts, opt => opt.Ignore());

            CreateMap<TblProducts, ProductDto>()
                .ForMember(dest => dest.RealPrice, opt => opt.MapFrom(src => src.Price))
                .ForMember(dest => dest.PriceAfterDiscount, opt => opt.Ignore())
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src =>
                    src.Category != null ? src.Category.Name : null))
                .ForMember(dest => dest.PhotoUrls, opt => opt.MapFrom(src =>
                    src.Photos == null ? new List<PhotoUrlWithId>() :
                    src.Photos.Where(p => p.IsDeleted != true)
                              .Select(p => new PhotoUrlWithId
                              {
                                  Id = p.Id,
                                  Url = p.PhotoPath
                              })));
        }
    }
}
