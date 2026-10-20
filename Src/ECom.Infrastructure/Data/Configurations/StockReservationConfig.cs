using Ecom.Core.Entities.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECom.Infrastructure.Data.Configurations
{
    public class StockReservationConfig : IEntityTypeConfiguration<TblStockReservation>
    {
        public void Configure(EntityTypeBuilder<TblStockReservation> builder)
        {
            builder.ToTable("TblStockReservations");

            builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            builder.Property(x => x.Quantity);

            builder.Property(x => x.Status)
                .HasConversion(
                    x => x.ToString(),
                    x => (ReservationStatus)System.Enum.Parse(typeof(ReservationStatus), x))
                .HasMaxLength(30);

            builder.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reservation -> Order, without a collection navigation on TblOrders.
            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            // A user can hold exactly one active (Pending/AttachedToOrder) reservation
            // per product. The database enforces it for non-active rows truncate to nothing:
            // only active rows participate in the uniqueness check.
            builder.HasIndex(x => new { x.UserId, x.ProductId })
                .IsUnique()
                .HasDatabaseName("IX_TblStockReservations_ActiveUserProduct")
                .HasFilter("[Status] IN ('Pending','AttachedToOrder')");
        }
    }
}