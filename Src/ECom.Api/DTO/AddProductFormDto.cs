using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;

using ECom.Api.Helper;

namespace ECom.Api.DTO
{
    public class AddProductFormDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }

        public List<IFormFile>? Files { get; set; }

        public DateTime? DiscountFrom { get; set; }
        public DateTime? DiscountTo { get; set; }
        public DiscountType? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountName { get; set; }
    }

    public static class AddProductFormExtensions
    {
        public static AddProductDto ToDto(this AddProductFormDto form)
        {
            var dto = new AddProductDto
            {
                Name = form.Name,
                Description = form.Description,
                Price = form.Price,
                CategoryId = form.CategoryId,
                Files = form.Files?.Select(FileParameterConverter.ToFileParameter).ToList()
            };

            if (form.DiscountFrom.HasValue && form.DiscountTo.HasValue && form.DiscountType.HasValue)
            {
                dto.Discount = new AddDiscountDto
                {
                    From = form.DiscountFrom.Value,
                    To = form.DiscountTo.Value,
                    DiscountType = form.DiscountType.Value,
                    Value = form.DiscountValue ?? 0,
                    Name = form.DiscountName
                };
            }

            return dto;
        }
    }
}
