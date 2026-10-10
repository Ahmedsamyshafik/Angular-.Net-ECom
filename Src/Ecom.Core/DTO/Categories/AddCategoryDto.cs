using Ecom.Core.DTO.Common;

namespace Ecom.Core.DTO.Categories
{
    public record AddCategoryDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<FileParameterDto>? Files { get; set; }
    }
}
