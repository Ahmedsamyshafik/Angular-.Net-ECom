namespace Ecom.Core.DTO.Products
{
    public class AddDiscountDto
    {
        public int productId { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TypeOfDescount { get; set; }
        public int val { get; set; }
        public string? name { get; set; }

    }
}
