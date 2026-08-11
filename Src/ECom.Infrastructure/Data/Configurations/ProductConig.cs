using Ecom.Core.Entities.Tbl_Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ECom.Infrastructure.Data.Configurations
{
    public class ProductConfig : IEntityTypeConfiguration<TblProducts>
    {
        public void Configure(EntityTypeBuilder<TblProducts> builder)
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
            //seeding
            builder.HasData(
                     new TblProducts { Id = 1, Name = "Product 1", Description = "Description for Product 1", Price = 10.99m, CategoryId = 1, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        }
    }
}
