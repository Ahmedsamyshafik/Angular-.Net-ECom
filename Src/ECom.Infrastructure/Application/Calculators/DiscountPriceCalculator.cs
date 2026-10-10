using Ecom.Core.Entities.Discount;
using Ecom.Core.Services;

namespace ECom.Infrastructure.Application.Calculators
{
    /// <summary>
    /// The only place discount math and the "active window" rule live. Reusing the same window expression
    /// that <see cref="Facades.DiscountFacade"/> uses when a discount is applied, evaluated at read time.
    /// </summary>
    public class DiscountPriceCalculator : IDiscountPriceCalculator
    {
        #region Private Fields

        /// <summary>Matches the decimal(18,2) precision the prices and discount values are stored with.</summary>
        private const int MoneyScale = 2;

        private const decimal OneHundred = 100m;

        #endregion

        #region Methods

        public decimal? CalculatePriceAfterDiscount(decimal realPrice, IEnumerable<TblDiscount>? discounts)
        {
            if (discounts is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;

            // Lowest resulting price wins when several discounts overlap, so the customer always gets the
            // best available price. Ties are broken by the newest discount (latest CreatedAt, then highest Id).
            decimal? best = null;
            TblDiscount? bestDiscount = null;

            foreach (var discount in discounts)
            {
                if (discount is null || discount.IsDeleted)
                {
                    continue;
                }

                if (!IsActiveNow(discount, now))
                {
                    continue;
                }

                var candidate = Apply(discount, realPrice);

                if (best is null || IsBetter(candidate, discount, best.Value, bestDiscount!))
                {
                    best = candidate;
                    bestDiscount = discount;
                }
            }

            if (best is null)
            {
                return null;
            }

            // A percentage above 100, or a fixed amount larger than the base price, would otherwise
            // produce a negative price. Floor it at zero.
            return Normalize(best.Value);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// The single definition of an active discount, mirroring DiscountFacade.ApplyCoreAsync.
        /// Both bounds are inclusive, exactly as when the discount is applied.
        /// </summary>
        private static bool IsActiveNow(TblDiscount discount, DateTime now)
            => discount.StartDate <= now && now <= discount.EndDate;

        private static decimal Apply(TblDiscount discount, decimal realPrice)
            => discount.Type switch
            {
                DiscountType.Percentage => realPrice - (realPrice * discount.Value / OneHundred),
                DiscountType.FixedAmount => realPrice - discount.Value,
                _ => realPrice
            };

        private static bool IsBetter(decimal candidate, TblDiscount candidateDiscount, decimal current, TblDiscount currentDiscount)
        {
            if (candidate != current)
            {
                return candidate < current;
            }

            // Same price: prefer the newer discount so the outcome is deterministic.
            if (candidateDiscount.CreatedAt != currentDiscount.CreatedAt)
            {
                return candidateDiscount.CreatedAt > currentDiscount.CreatedAt;
            }

            return candidateDiscount.Id > currentDiscount.Id;
        }

        private static decimal Normalize(decimal price)
        {
            var rounded = Math.Round(price, MoneyScale, MidpointRounding.AwayFromZero);

            return rounded < 0m ? 0m : rounded;
        }

        #endregion
    }
}