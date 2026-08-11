using Ecom.Core.Entities.General;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Enum;

namespace ECom.Infrastructure.Repositores
{
    public class DiscountRepo : GenericRepo<TblDiscount, int>, IDiscountRepo
    {
        public DiscountRepo(AppDbContext context) : base(context)
        {
        }
        //Adding Discound First 


        public async Task<TblDiscount> AddDiscountAsync(DateTime From, DateTime To, int TypeOfDiscount, int val, string? Name)
        {
            var now = DateTime.UtcNow;

            bool isActiveNow = From <= now && now <= To;
            string DiscoundType = TypeOfDiscount == 1 ? StaticEnums.percentage.ToString() : StaticEnums.value.ToString();

            var disDB = new TblDiscount()
            {
                CreatedAt = DateTime.UtcNow,
                CreatedBy = 111,
                StartDate = From,
                EndDate = To,
                IsActive = isActiveNow,
                Name = Name ?? "",
                Type = DiscoundType
            };
            if (TypeOfDiscount == 1)//percentage
            {
                disDB.Percentage = val;
            }
            else
            {
                disDB.Value = val;
            }
            await _context.AddAsync(disDB);
            return disDB;
        }

    }
}


