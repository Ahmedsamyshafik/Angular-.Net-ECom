using Microsoft.AspNetCore.Http;

namespace Ecom.Core.DTO.Products
{
    public record UpdateProductDto
    {
        public int id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }

        public List<string>? CurrentImgPaths { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }// List of IDs of existing photos to keep not change
        public List<IFormFile>? NewPhotos { get; set; } // List of new photos to add>

    }
}
