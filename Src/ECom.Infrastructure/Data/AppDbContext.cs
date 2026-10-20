using System.Reflection;

using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Stock;
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
        public DbSet<TblOrders> tblOrders { get; set; }
        public DbSet<TblOrderItem> tblOrderItems { get; set; }
        public DbSet<TblShippingAddress> tblShippingAddresses { get; set; }
        public DbSet<TblDeliveryMethod> tblDeliveryMethods { get; set; }
        public DbSet<TblStockReservation> TblStockReservations { get; set; }


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

            // Single address table (tblShippingAddresses): the signed-in user's saved
            // address shares the table used by the order shipping addresses. The user
            // side is identified by AppUserId, the order side by OrderId.
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.ShippingAddress)
                .WithOne()
                .HasForeignKey<TblShippingAddress>(x => x.AppUserId);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
