using Ecom.Core.Entities.Tbl_Product;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly()); // Get it's Implementation from IEntityTypeConfiguration<T> interface     s
        }
    }
}
