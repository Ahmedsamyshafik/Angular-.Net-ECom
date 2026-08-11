using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.General;
using Ecom.Core.Entities.Tbl_Product;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ECom.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<TblProducts> TblProducts { get; set; }
        public DbSet<TblCategories> tblCategories { get; set; }
        public DbSet<TblPhoto> tblPhotos { get; set; }
        public DbSet<TblDiscount> tblDiscounds { get; set; }
        public DbSet<TblProductDiscount> tblProductDiscounts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            //discount
            //modelBuilder.Entity<TblProductDiscount>()
            //        .HasKey(x => new { x.productId, x.DiscountId });  commented this to depend on BaseKey

            modelBuilder.Entity<TblProductDiscount>()
                    .HasOne(x => x.Product)
                    .WithMany(x => x.ProductDiscounts)
                    .HasForeignKey(x => x.productId);

            modelBuilder.Entity<TblProductDiscount>()
                .HasOne(x => x.Discount)
                .WithMany(x => x.productDiscounts)
                .HasForeignKey(x => x.DiscountId);


            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly()); // Get it's Implementation from IEntityTypeConfiguration<T> interface     s
        }
    }
}
