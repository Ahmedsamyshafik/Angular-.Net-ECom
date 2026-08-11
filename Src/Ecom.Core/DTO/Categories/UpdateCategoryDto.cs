using Microsoft.AspNetCore.Http;
using Ecom.Core.DTO.Products;

namespace Ecom.Core.DTO.Categories
{
    public record UpdateCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }// List of IDs of existing photos to keep not change
        public List<IFormFile>? NewPhotos { get; set; } // List of new photos to add>
    }
    public record CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<PhotoUrlWithId> Photos { get; set; } = new();
    }
}
