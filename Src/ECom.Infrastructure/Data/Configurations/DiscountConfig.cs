using Ecom.Core.Entities.Discount;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class DiscountConfig : IEntityTypeConfiguration<TblDiscount>
    {
        public void Configure(EntityTypeBuilder<TblDiscount> builder)
        {
            builder.Property(x => x.Name).HasMaxLength(100);
            builder.Property(x => x.Value).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Type).HasConversion<int>();
        }
    }
}
