namespace Ecom.Core.DTO.CartDto
{
    public class UpdateCartItemDto
    {
        // The cart owner is taken from the authenticated token, never from the client.
        public int productId { get; set; }
        public int quantity { get; set; }
    }
}
