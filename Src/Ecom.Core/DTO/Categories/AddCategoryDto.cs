using Microsoft.AspNetCore.Http;

namespace Ecom.Core.DTO.Categories
{
    public record AddCategoryDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<IFormFile>? files { get; set; }
    }
}
