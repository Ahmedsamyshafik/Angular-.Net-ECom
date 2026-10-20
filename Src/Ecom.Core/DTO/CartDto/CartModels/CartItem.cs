namespace Ecom.Core.DTO.CartDto.CartModels
{
    public class CartItem
    {

        public int ProductId { get; set; }

        public string ProductName { get; set; }

        public string ProductDescription { get; set; }

        public List<string> productImgsUrl { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public DateTime? PriceExpiresAt { get; set; }

        public decimal Total => UnitPrice * Quantity;
    }
}
