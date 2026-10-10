namespace Ecom.Core.DTO.CartDto
{
    public class ProductCartInfoDto
    {
        public string ProductName { get; set; } = string.Empty;
        public List<string>? productImgsUrl { get; set; }
        public string? ProductDescription { get; set; }
        public int Qunatity { get; set; }
        public int ProductId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal FinalPrice { get; set; }
        public DateTime? PriceExpiresAt { get; set; }
        public int AvailableQuantity { get; set; }
    }

}
