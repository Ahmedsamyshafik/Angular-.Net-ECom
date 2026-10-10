using Ecom.Core.DTO.Common;

namespace Ecom.Core.DTO.Products
{
    public record UpdateProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public byte[]? RowVersion { get; set; }

        public List<string>? CurrentImgPaths { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }
        public List<FileParameterDto>? NewPhotos { get; set; }
    }
}
