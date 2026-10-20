namespace Ecom.Core.DTO.CartDto
{
    public class UpdateCartRequest
    {
        // Set server-side from the authenticated token; not required from the client.
        public string? userId { get; set; }
        public List<CartItemRequest> Items { get; set; }
    }
}
