namespace ECom.Api.DTO
{
    public class AddProductToCartDto
    {
        // productid , userID,Quentity ,
        public int productId { get; set; }
        public int Quentity { get; set; }
        public string UserId { get; set; }
    }
}
