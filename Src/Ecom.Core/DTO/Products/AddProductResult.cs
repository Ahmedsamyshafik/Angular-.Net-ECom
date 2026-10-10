namespace Ecom.Core.DTO.Products
{
    public sealed record AddProductResult(ProductDto Product, IReadOnlyList<string> StoredPhotoPaths);
}
