using Ecom.Core.Entities.Order;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class OrderConfig : IEntityTypeConfiguration<TblOrders>
    {
        public void Configure(EntityTypeBuilder<TblOrders> builder)
        {
            // One address table (tblShippingAddresses) now stores both order and user
            // addresses, so the shipping address is a regular one-to-one dependent
            // (OrderId FK) instead of an owned entity with its own PK.
            builder.HasOne(x => x.shippingAddress)
                .WithOne()
                .HasForeignKey<TblShippingAddress>(x => x.OrderId);//one to one with shippingAdress
            builder.HasMany(x => x.orderItems).WithOne(x => x.tblOrder).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade); // one to many with OrderItems
            builder.Property(x => x.status)
         .HasConversion(
             o => o.ToString(),
             o => (Status)System.Enum.Parse(typeof(Status), o));
            builder.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
        }
    }
}
