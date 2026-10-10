using AutoMapper;

using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

namespace ECom.Infrastructure.Application.Facades
{
    public class DiscountFacade : IDiscountFacade
    {
        #region Private Fields

        private readonly IProductRepo _productRepo;
        private readonly IDiscountRepo _discountRepo;
        private readonly IDiscountProductRepo _discountProductRepo;
        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public DiscountFacade(
            IProductRepo productRepo,
            IDiscountRepo discountRepo,
            IDiscountProductRepo discountProductRepo,
            IMapper mapper)
        {
            _productRepo = productRepo;
            _discountRepo = discountRepo;
            _discountProductRepo = discountProductRepo;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public async Task ApplyDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default)
        {
            if (discount.ProductIds == null || discount.ProductIds.Count == 0)
            {
                throw new BadRequestException("Provide at least one product id.");
            }

            await ApplyCoreAsync(discount, cancellationToken);
        }

        public async Task ApplyCategoryDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default)
        {
            if (!discount.CategoryId.HasValue)
            {
                throw new BadRequestException("Provide a category id.");
            }

            var products = await _productRepo.GetAllWithDeletedAsync(
                predicate: x => x.CategoryId == discount.CategoryId.Value && x.IsDeleted == false,
                cancellationToken: cancellationToken);

            if (products == null || products.Count == 0)
            {
                throw new NotFoundException("No products found in this category.");
            }

            discount.ProductIds = products.Select(p => p.Id).ToList();

            await ApplyCoreAsync(discount, cancellationToken);
        }

        private async Task ApplyCoreAsync(AddDiscountDto discount, CancellationToken cancellationToken)
        {
            var entity = _mapper.Map<TblDiscount>(discount);
            entity.CreatedAt = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            entity.IsActive = entity.StartDate <= now && now <= entity.EndDate;

            await _discountRepo.AddAsync(entity, cancellationToken);
            await _discountProductRepo.AddDiscountToProductsAsync(entity, discount.ProductIds ?? new List<int>(), cancellationToken);
        }

        #endregion
    }
}
