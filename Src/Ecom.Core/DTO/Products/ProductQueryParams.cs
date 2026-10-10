namespace Ecom.Core.DTO.Products
{
    public class ProductQueryParams
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortBy { get; set; } = "id";
        public string? SortOrder { get; set; } = "asc";
    }
}