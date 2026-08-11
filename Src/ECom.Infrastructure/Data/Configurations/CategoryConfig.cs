using Ecom.Core.Entities.Tbl_Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class CategoryConfig : IEntityTypeConfiguration<TblCategories>
    {
        public void Configure(EntityTypeBuilder<TblCategories> builder)
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Id).IsRequired();
            //seeding
            builder.HasData(
                  new TblCategories { Id = 1, Name = "Electronics", Description = "Electronic devices and gadgets", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
              );
        }

    }
}
