using Ecom.Core.DTO.Common;

namespace Ecom.Core.DTO.Categories
{
    public record UpdateCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[]? RowVersion { get; set; }

        public List<string>? CurrentImgPaths { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }
        public List<FileParameterDto>? NewPhotos { get; set; }
    }
}
