using Ecom.Core.Entities.Discount;
using ECom.Infrastructure.Application.Calculators;

namespace ECom.Api.Tests.Services
{
    public class DiscountPriceCalculatorTests
    {
        private readonly DiscountPriceCalculator _calculator = new DiscountPriceCalculator();

        private static TblDiscount Discount(
            DiscountType type,
            decimal value,
            int id = 1,
            DateTime? from = null,
            DateTime? to = null,
            bool isDeleted = false)
        {
            return new TblDiscount
            {
                Id = id,
                Type = type,
                Value = value,
                StartDate = from ?? DateTime.UtcNow.AddDays(-1),
                EndDate = to ?? DateTime.UtcNow.AddDays(1),
                IsActive = true,
                IsDeleted = isDeleted
            };
        }

        #region No discount

        [Fact]
        public void CalculatePriceAfterDiscount_WithNullCollection_ShouldReturnNull()
        {
            var result = _calculator.CalculatePriceAfterDiscount(1000m, null);

            Assert.Null(result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithEmptyCollection_ShouldReturnNull()
        {
            var result = _calculator.CalculatePriceAfterDiscount(1000m, Array.Empty<TblDiscount>());

            Assert.Null(result);
        }

        #endregion

        #region Percentage

        [Fact]
        public void CalculatePriceAfterDiscount_WithActivePercentage_ShouldSubtractThePercentage()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.Percentage, 20m) });

            Assert.Equal(800m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithFullPercentage_ShouldReturnZero()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.Percentage, 100m) });

            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithPercentageProducingFractions_ShouldRoundToTwoDecimals()
        {
            // 999.99 - 16.5% = 834.99165...
            var result = _calculator.CalculatePriceAfterDiscount(
                999.99m,
                new[] { Discount(DiscountType.Percentage, 16.5m) });

            Assert.Equal(834.99m, result);
        }

        #endregion

        #region Fixed amount

        [Fact]
        public void CalculatePriceAfterDiscount_WithActiveFixedAmount_ShouldSubtractTheAmount()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.FixedAmount, 250m) });

            Assert.Equal(750m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithFixedAmountEqualToPrice_ShouldReturnZero()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.FixedAmount, 1000m) });

            Assert.Equal(0m, result);
        }

        #endregion

        #region Window boundaries

        [Fact]
        public void CalculatePriceAfterDiscount_WhenNotStartedYet_ShouldReturnNull()
        {
            var discount = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddDays(1),
                to: DateTime.UtcNow.AddDays(5));

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Null(result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenAlreadyExpired_ShouldReturnNull()
        {
            var discount = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddDays(-5),
                to: DateTime.UtcNow.AddDays(-1));

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Null(result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenStartDateIsJustBeforeNow_ShouldBeActive()
        {
            // Both bounds are inclusive, matching DiscountFacade.ApplyCoreAsync, so a window that opened
            // a moment ago is already live. The one-second margin keeps this independent of the clock
            // ticking between the test and the calculator's own DateTime.UtcNow read.
            var discount = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddSeconds(-1),
                to: DateTime.UtcNow.AddDays(1));

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Equal(800m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenEndDateIsJustAfterNow_ShouldBeActive()
        {
            var discount = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddDays(-1),
                to: DateTime.UtcNow.AddSeconds(1));

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Equal(800m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenStartDateIsJustAfterNow_ShouldNotBeActiveYet()
        {
            var discount = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddSeconds(1),
                to: DateTime.UtcNow.AddDays(1));

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Null(result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenIsActiveColumnIsStale_ShouldTrustTheDateWindow()
        {
            // A discount created with a future start date keeps IsActive == false forever, and an expired
            // one keeps IsActive == true forever. The date window is what actually decides.
            var futureButFlaggedActive = Discount(
                DiscountType.Percentage,
                20m,
                from: DateTime.UtcNow.AddDays(1),
                to: DateTime.UtcNow.AddDays(5));
            futureButFlaggedActive.IsActive = true;

            var expiredButFlaggedInactive = Discount(
                DiscountType.Percentage,
                20m,
                id: 2,
                from: DateTime.UtcNow.AddDays(-5),
                to: DateTime.UtcNow.AddDays(-1));
            expiredButFlaggedInactive.IsActive = false;

            Assert.Null(_calculator.CalculatePriceAfterDiscount(
                1000m, new[] { futureButFlaggedActive, expiredButFlaggedInactive }));
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WhenDiscountIsSoftDeleted_ShouldReturnNull()
        {
            var discount = Discount(DiscountType.Percentage, 20m, isDeleted: true);

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { discount });

            Assert.Null(result);
        }

        #endregion

        #region Overlapping discounts

        [Fact]
        public void CalculatePriceAfterDiscount_WithSeveralActiveDiscounts_ShouldReturnTheLowestPrice()
        {
            var discounts = new[]
            {
                Discount(DiscountType.Percentage, 10m, id: 1),
                Discount(DiscountType.Percentage, 40m, id: 2),
                Discount(DiscountType.FixedAmount, 100m, id: 3)
            };

            // 1000 - 40% = 600 is the best of 900, 600 and 900.
            var result = _calculator.CalculatePriceAfterDiscount(1000m, discounts);

            Assert.Equal(600m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithEqualPrices_ShouldReturnThatPrice()
        {
            var older = Discount(DiscountType.Percentage, 20m, id: 5);
            older.CreatedAt = DateTime.UtcNow.AddDays(-3);

            var newer = Discount(DiscountType.FixedAmount, 200m, id: 9);
            newer.CreatedAt = DateTime.UtcNow.AddDays(-1);

            var result = _calculator.CalculatePriceAfterDiscount(1000m, new[] { older, newer });

            // Both produce 800. The winner is picked by CreatedAt then Id, but the calculator only
            // returns the price, so that choice is not observable here; this pins the value.
            Assert.Equal(800m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithEqualPrices_ShouldReturnTheSamePriceRegardlessOfOrder()
        {
            var older = Discount(DiscountType.Percentage, 20m, id: 5);
            older.CreatedAt = DateTime.UtcNow.AddDays(-3);

            var newer = Discount(DiscountType.FixedAmount, 200m, id: 9);
            newer.CreatedAt = DateTime.UtcNow.AddDays(-1);

            var forwards = _calculator.CalculatePriceAfterDiscount(1000m, new[] { older, newer });
            var backwards = _calculator.CalculatePriceAfterDiscount(1000m, new[] { newer, older });

            // The input order must never change the answer.
            Assert.Equal(forwards, backwards);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_ShouldIgnoreInactiveDiscountsEvenWhenTheyWouldWin()
        {
            var discounts = new[]
            {
                Discount(DiscountType.Percentage, 10m, id: 1),
                Discount(
                    DiscountType.Percentage,
                    90m,
                    id: 2,
                    from: DateTime.UtcNow.AddDays(1),
                    to: DateTime.UtcNow.AddDays(5))
            };

            var result = _calculator.CalculatePriceAfterDiscount(1000m, discounts);

            Assert.Equal(900m, result);
        }

        #endregion

        #region Negative results

        [Fact]
        public void CalculatePriceAfterDiscount_WithPercentageAboveOneHundred_ShouldClampToZero()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.Percentage, 200m) });

            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithFixedAmountLargerThanPrice_ShouldClampToZero()
        {
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.FixedAmount, 5000m) });

            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithNegativePrice_ShouldNotReturnANegativeAmount()
        {
            // 100 - 99.999 = 0.001, which rounds to 0.00 at the stored decimal(18,2) precision.
            var result = _calculator.CalculatePriceAfterDiscount(
                100m,
                new[] { Discount(DiscountType.FixedAmount, 99.999m) });

            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePriceAfterDiscount_WithFixedAmountLeavingAFraction_ShouldRoundDownToTwoDecimals()
        {
            // 1000 - 199.999 = 800.001 -> 800.00
            var result = _calculator.CalculatePriceAfterDiscount(
                1000m,
                new[] { Discount(DiscountType.FixedAmount, 199.999m) });

            Assert.Equal(800m, result);
        }

        #endregion
    }
}