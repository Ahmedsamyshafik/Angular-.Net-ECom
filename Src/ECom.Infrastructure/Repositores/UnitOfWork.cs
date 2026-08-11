using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECom.Infrastructure.Repositores
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public IProductRepo ProductRepo  { get; }

        public ICategoryRepo CategoryRepo { get; }

        public IPhotoRepo PhotoRepo { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context=context;
            CategoryRepo = new CategoryRepo(_context);
            ProductRepo = new ProductRepo(_context);
            PhotoRepo = new PhotoRepo(_context);
        }
    }
}
