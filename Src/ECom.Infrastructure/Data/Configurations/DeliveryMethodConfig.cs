using Ecom.Core.Entities.Order;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class DeliveryMethodConfig : IEntityTypeConfiguration<TblDeliveryMethod>
    {
        public void Configure(EntityTypeBuilder<TblDeliveryMethod> builder)
        {
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
            builder.HasData(
                new TblDeliveryMethod
                {
                    Id = 1,
                    CreatedAt = new DateTime(2026, 1, 1),
                    Name = "option one ",
                    Price = 10,
                    DeliveryTime = "1",
                    Description = "desc for option one"
                }, new TblDeliveryMethod
                {
                    Id = 2,
                    CreatedAt = new DateTime(2026, 1, 1),
                    Name = "option two ",
                    Price = 20,
                    DeliveryTime = "2",
                    Description = "desc for option two"
                }, new TblDeliveryMethod
                {
                    Id = 3,
                    CreatedAt = new DateTime(2026, 1, 1),
                    Name = "option three ",
                    Price = 30,
                    DeliveryTime = "3",
                    Description = "desc for option three"
                }
                );


        }
    }
}
