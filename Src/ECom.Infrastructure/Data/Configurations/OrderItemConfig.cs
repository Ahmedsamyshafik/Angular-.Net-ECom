using Ecom.Core.Entities.Order;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class OrderItemConfig : IEntityTypeConfiguration<TblOrderItem>
    {
        public void Configure(EntityTypeBuilder<TblOrderItem> builder)
        {
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");

        }
    }
}
