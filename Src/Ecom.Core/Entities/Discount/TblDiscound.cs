using Ecom.Core.Entities.Discount;

namespace Ecom.Core.Entities.General
{
    public class TblDiscount:BaseEntity<int>
    {
        public string? Name { get; set; }

        //Percentage?FixedAmount?
        public string Type { get; set; }
        public decimal Value { get; set; }
        public decimal Percentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }

        public ICollection<TblProductDiscount> productDiscounts { get; set; }=new List<TblProductDiscount>();

    }
}
