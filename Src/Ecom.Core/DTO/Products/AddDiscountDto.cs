using Ecom.Core.Entities.Discount;

namespace Ecom.Core.DTO.Products
{
    public class AddDiscountDto
    {
        public int? CategoryId { get; set; }
        public List<int>? ProductIds { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal Value { get; set; }
        public string? Name { get; set; }
    }
}
