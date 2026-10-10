namespace Ecom.Core.DTO.Products
{
    public record ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Original/base price of the product, before any discount. This is the value stored in
        /// <c>TblProducts.Price</c> and the value that <c>minPrice</c>/<c>maxPrice</c> filtering and
        /// <c>sortBy=price</c> operate on. Mirrors the input contract: Add/Update still send <c>Price</c>.
        /// </summary>
        public decimal RealPrice { get; set; }

        /// <summary>
        /// Final price after applying the currently active discount, or <c>null</c> when the product
        /// has no active discount. Resolved by IDiscountPriceCalculator, never by the mapping profile.
        /// </summary>
        public decimal? PriceAfterDiscount { get; set; }

        public string CategoryName { get; set; }

        public int CategoryId { get; set; }

        public string? Description { get; set; }
        public byte[]? RowVersion { get; set; }
        public List<PhotoUrlWithId> PhotoUrls { get; set; } = new();
    }
}
