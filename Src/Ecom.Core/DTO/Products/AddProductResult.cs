using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.DTO.Products
{
    /// <summary>
    /// Result of adding a product.
    /// <para>
    /// <see cref="Entity"/> carries the tracked (but not yet persisted) <see cref="TblProducts"/>
    /// instance. The discount-on-add flow needs it to link the discount through the product
    /// navigation, because the database-generated <see cref="ProductDto.Id"/> is still 0 until
    /// <c>SaveChanges</c> runs in the unit of work.
    /// </para>
    /// </summary>
    public sealed record AddProductResult(
        ProductDto Product,
        IReadOnlyList<string> StoredPhotoPaths,
        TblProducts? Entity = null);
}
