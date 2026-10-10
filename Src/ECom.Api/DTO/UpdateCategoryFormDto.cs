using Ecom.Core.DTO.Categories;
using ECom.Api.Helper;

namespace ECom.Api.DTO
{
    public class UpdateCategoryFormDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? RowVersion { get; set; }

        public List<int>? ExistingPhotoIds { get; set; }
        public List<IFormFile>? NewPhotos { get; set; }
    }

    public static class UpdateCategoryFormExtensions
    {
        public static UpdateCategoryDto ToDto(this UpdateCategoryFormDto form)
        {
            return new UpdateCategoryDto
            {
                Id = form.Id,
                Name = form.Name,
                Description = form.Description,
                RowVersion = string.IsNullOrEmpty(form.RowVersion) ? null : Convert.FromBase64String(form.RowVersion),
                ExistingPhotoIds = form.ExistingPhotoIds,
                NewPhotos = form.NewPhotos?.Select(FileParameterConverter.ToFileParameter).ToList()
            };
        }
    }
}
