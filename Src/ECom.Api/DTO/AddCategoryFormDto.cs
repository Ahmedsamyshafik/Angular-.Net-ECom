using Ecom.Core.DTO.Categories;
using ECom.Api.Helper;

namespace ECom.Api.DTO
{
    public class AddCategoryFormDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<IFormFile>? Files { get; set; }
    }

    public static class AddCategoryFormExtensions
    {
        public static AddCategoryDto ToDto(this AddCategoryFormDto form)
        {
            return new AddCategoryDto
            {
                Name = form.Name,
                Description = form.Description,
                Files = form.Files?.Select(FileParameterConverter.ToFileParameter).ToList()
            };
        }
    }
}
