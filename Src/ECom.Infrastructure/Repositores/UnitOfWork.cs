using Ecom.Core.Interfaces;
using Ecom.Core.Services;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositores.Services;
using Microsoft.AspNetCore.Hosting;

namespace ECom.Infrastructure.Repositores
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public IProductRepo ProductRepo { get; }

        public ICategoryRepo CategoryRepo { get; }

        public IPhotoRepo PhotoRepo { get; }
        public IDiscountRepo DiscountRepo { get; }
        public IDiscountProductRepo DiscountProductRepo { get; }
        public IImageManagementService ImageManagementService { get; }

        public UnitOfWork(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
            CategoryRepo = new CategoryRepo(_context);
            ProductRepo = new ProductRepo(_context);
            PhotoRepo = new PhotoRepo(_context);
            DiscountProductRepo = new DiscountProductRepo(_context);
            DiscountRepo = new DiscountRepo(_context);
            ImageManagementService = new ImageManagementService(new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Directory.GetCurrentDirectory()), this, _environment);
        }

        public async Task Commit()
        {
            await _context.SaveChangesAsync();
        }
    }
}
