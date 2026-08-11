using AutoMapper;
using Ecom.Core.DTO.Categories;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

namespace ECom.Api.Mapping
{
    public class CategoryMapping : Profile
    {
        public CategoryMapping()
        {
            CreateMap<AddCategoryDto, TblCategories>().ReverseMap();
            CreateMap<UpdateCategoryDto, TblCategories>().ReverseMap();

            CreateMap<TblCategories, CategoryDto>()
                .ForMember(dest => dest.Photos, opt => opt.MapFrom(src =>
                    src.Photos.Where(p => p.IsDeleted != true)
                              .Select(p => new PhotoUrlWithId
                              {
                                  Id = p.Id,
                                  Url = p.PhotoPath
                              }).ToList()
                ));
        }
    }
}
