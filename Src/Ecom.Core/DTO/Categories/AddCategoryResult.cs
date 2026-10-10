namespace Ecom.Core.DTO.Categories
{
    public sealed record AddCategoryResult(CategoryDto Category, IReadOnlyList<string> StoredPhotoPaths);
}
