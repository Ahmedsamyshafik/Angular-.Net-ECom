using Ecom.Core.DTO.Common;
using Ecom.Core.DTO.Photo;

namespace Ecom.Core.DTO.Products
{
    public record AddProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }

        public List<FileParameterDto>? Files { get; set; }

        public List<PhotoDto>? Photos { get; set; }

        public AddDiscountDto? Discount { get; set; }
    }
}
