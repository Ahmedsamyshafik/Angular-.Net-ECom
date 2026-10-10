namespace Ecom.Core.Entities.Discount
{
    public class TblDiscount : BaseEntity<int>
    {
        public string? Name { get; set; }

        public DiscountType Type { get; set; }
        public decimal Value { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }

        public ICollection<TblProductDiscount> ProductDiscounts { get; set; } = new List<TblProductDiscount>();
    }
}
