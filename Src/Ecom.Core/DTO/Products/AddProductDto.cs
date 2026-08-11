using Ecom.Core.DTO.Photo;
using Microsoft.AspNetCore.Http;

namespace Ecom.Core.DTO.Products
{
    public record AddProductDto
    {
        public int id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }

        public List<IFormFile>? files { get; set; }

        public List<PhotoDto>? Photo { get; set; }

    }
}
