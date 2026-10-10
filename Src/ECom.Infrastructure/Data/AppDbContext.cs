using System.Reflection;

using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Entities.User;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<TblProducts> TblProducts { get; set; }
        public DbSet<TblCategories> TblCategories { get; set; }
        public DbSet<TblPhoto> TblPhotos { get; set; }
        public DbSet<TblDiscount> TblDiscounts { get; set; }
        public DbSet<TblProductDiscount> TblProductDiscounts { get; set; }
        public DbSet<Address> Address { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TblProductDiscount>()
                    .HasOne(x => x.Product)
                    .WithMany(x => x.ProductDiscounts)
                    .HasForeignKey(x => x.ProductId);

            modelBuilder.Entity<TblProductDiscount>()
                .HasOne(x => x.Discount)
                .WithMany(x => x.ProductDiscounts)
                .HasForeignKey(x => x.DiscountId);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
