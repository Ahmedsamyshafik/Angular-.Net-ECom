using Ecom.Core.Entities.Discount;

namespace Ecom.Core.Services
{
    /// <summary>
    /// Single source of truth for "is this discount active right now" and "what price does it produce".
    /// The active window matches the rule already used when a discount is applied
    /// (<c>StartDate &lt;= now &amp;&amp; now &lt;= EndDate</c>), but it is evaluated at read time on purpose:
    /// <c>TblDiscount.IsActive</c> is a write-time snapshot set once when the discount is created and never
    /// refreshed, so it cannot answer "is this active now" (a discount created with a future start date stays
    /// inactive forever, and an expired one stays active forever).
    /// </summary>
    public interface IDiscountPriceCalculator
    {
        /// <summary>
        /// Returns the discounted price for <paramref name="realPrice"/>, or <c>null</c> when none of
        /// <paramref name="discounts"/> is active. <paramref name="discounts"/> may contain inactive and
        /// soft-deleted discounts; this method owns the filtering.
        /// </summary>
        decimal? CalculatePriceAfterDiscount(decimal realPrice, IEnumerable<TblDiscount>? discounts);
    }
}