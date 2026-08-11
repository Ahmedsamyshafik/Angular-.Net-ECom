namespace Ecom.Core.DTO.Products
{
    public record ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }

        // بنرجع اسم/تفاصيل الكاتيجوري فقط، ومش بنحط جوه الكاتيجوري ليستة Products!
        public string CategoryName { get; set; }
        public List<PhotoUrlWithId> PhotoUrls { get; set; } = new();
    }

    public record PhotoUrlWithId
    {
        public int Id { get; set; }
        public string Url { get; set; }
    }
}
