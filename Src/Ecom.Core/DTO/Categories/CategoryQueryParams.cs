namespace Ecom.Core.DTO.Categories
{
    public class CategoryQueryParams
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? HasProducts { get; set; }
        public string? SortBy { get; set; } = "id";
        public string? SortOrder { get; set; } = "asc";
    }
}