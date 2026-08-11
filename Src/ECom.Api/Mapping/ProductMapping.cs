using AutoMapper;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

namespace ECom.Api.Mapping
{
    public class ProductMapping : Profile
    {
        public ProductMapping()
        {
            CreateMap<TblProducts, AddProductDto>().ReverseMap();
            CreateMap<UpdateProductDto,TblProducts>().ReverseMap()
                .ForMember(dest=>dest.CurrentImgPaths, opt => opt.MapFrom(src => src.Photos.Select(p => p.PhotoPath)));

            //CreateMap<TblPhoto, PhotoUrlWithId>()
            //.ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            //.ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.PhotoPath));

            //CreateMap<TblProducts, ProductDto>()
            //    .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            //    .ForMember(dest => dest.PhotoUrls, opt => opt.MapFrom(src => src.Photos));

            CreateMap<TblProducts, ProductDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.PhotoUrls, opt => opt.MapFrom(src =>
                    src.Photos.Select(p => new PhotoUrlWithId
                    {
                        Id = p.Id,
                        Url = p.PhotoPath
                    }).ToList()
                ));
        }
    }
}
