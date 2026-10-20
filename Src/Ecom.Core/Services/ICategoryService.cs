using Ecom.Core.DTO.Categories;
using Ecom.Core.Pagination;

namespace Ecom.Core.Services
{
    public interface ICategoryService
    {
        Task<Pagination<CategoryDto>> GetPageAsync(CategoryQueryParams query, CancellationToken cancellationToken = default);

        Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<AddCategoryResult> AddAsync(AddCategoryDto category, string systemUserId, CancellationToken cancellationToken = default);

        Task CompensateAsync(AddCategoryResult result, CancellationToken cancellationToken = default);

        Task<CategoryDto> UpdateAsync(UpdateCategoryDto category, string systemUserId, CancellationToken cancellationToken = default);

        Task DeleteAsync(int id, string systemUserId, CancellationToken cancellationToken = default);
    }
}
