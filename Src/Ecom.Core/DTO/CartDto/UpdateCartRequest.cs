namespace Ecom.Core.DTO.CartDto
{
    public class UpdateCartRequest
    {
        public string userId { get; set; }
        public List<CartItemRequest> Items { get; set; }
    }
}
