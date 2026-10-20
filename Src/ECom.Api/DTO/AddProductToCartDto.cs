namespace ECom.Api.DTO
{
    public class AddProductToCartDto
    {
        // The cart owner is taken from the authenticated token, never from the client.
        public int productId { get; set; }
        public int Quentity { get; set; }
    }
}
