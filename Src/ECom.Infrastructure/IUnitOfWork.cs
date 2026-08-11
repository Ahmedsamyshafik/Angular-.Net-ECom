using Ecom.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECom.Infrastructure
{
    public interface IUnitOfWork
    {

        public IProductRepo ProductRepo { get; }
        public ICategoryRepo CategoryRepo { get; }
        public IPhotoRepo PhotoRepo { get; }

    }
}
