namespace Ecom.Core.CartModels
{
    public class Cart
    {
        public string UserId { get; set; }

        public List<CartItem> Items { get; set; } = [];

        public decimal Total => Items.Sum(x => x.Total);
    }
}
