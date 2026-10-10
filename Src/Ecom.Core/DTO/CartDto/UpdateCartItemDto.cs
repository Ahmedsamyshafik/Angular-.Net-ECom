namespace Ecom.Core.DTO.CartDto
{
    public class UpdateCartItemDto
    {
        public string userId { get; set; }
        public int productId { get; set; }
        public int quantity { get; set; }
    }
}
