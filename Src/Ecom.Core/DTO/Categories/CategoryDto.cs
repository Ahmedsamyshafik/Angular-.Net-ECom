using Ecom.Core.DTO.Products;

namespace Ecom.Core.DTO.Categories
{
    public record CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[]? RowVersion { get; set; }
        public List<PhotoUrlWithId> Photos { get; set; } = new();
    }
}
