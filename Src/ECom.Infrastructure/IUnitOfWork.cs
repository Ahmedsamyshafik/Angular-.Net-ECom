using Ecom.Core.Interfaces;
using Ecom.Core.Services;

namespace ECom.Infrastructure
{
    public interface IUnitOfWork
    {

        public IProductRepo ProductRepo { get; }
        public ICategoryRepo CategoryRepo { get; }
        public IPhotoRepo PhotoRepo { get; }
        public IImageManagementService ImageManagementService { get; }
        public IDiscountProductRepo DiscountProductRepo { get; }
        public IDiscountRepo DiscountRepo { get; }

        public Task Commit();

    }
}
