using Ecom.Core.DTO.Common;
using Ecom.Core.DTO.Products;
using ECom.Api.Helper;

namespace ECom.Api.DTO
{
    public class UpdateProductFormDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public string? RowVersion { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }
        public List<IFormFile>? NewPhotos { get; set; }
    }

    public static class UpdateProductFormExtensions
    {
        public static UpdateProductDto ToDto(this UpdateProductFormDto form)
        {
            return new UpdateProductDto
            {
                Id = form.Id,
                Name = form.Name,
                Description = form.Description,
                Price = form.Price,
                CategoryId = form.CategoryId,
                RowVersion = string.IsNullOrEmpty(form.RowVersion) ? null : Convert.FromBase64String(form.RowVersion),
                ExistingPhotoIds = form.ExistingPhotoIds,
                NewPhotos = form.NewPhotos?.Select(FileParameterConverter.ToFileParameter).ToList()
            };
        }
    }
}
