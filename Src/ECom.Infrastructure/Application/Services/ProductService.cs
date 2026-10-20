using AutoMapper;

using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;

using ECom.Infrastructure.Enum;

namespace ECom.Infrastructure.Application.Services
{
    public class ProductService : IProductService
    {
        #region Private Fields

        private readonly IProductRepo _productRepo;
        private readonly ICategoryRepo _categoryRepo;
        private readonly IPhotoRepo _photoRepo;
        private readonly IDiscountProductRepo _discountProductRepo;
        private readonly IImageManagementService _imageManagementService;
        private readonly IDiscountPriceCalculator _discountPriceCalculator;
        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public ProductService(
            IProductRepo productRepo,
            ICategoryRepo categoryRepo,
            IPhotoRepo photoRepo,
            IDiscountProductRepo discountProductRepo,
            IImageManagementService imageManagementService,
            IDiscountPriceCalculator discountPriceCalculator,
            IMapper mapper)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _photoRepo = photoRepo;
            _discountProductRepo = discountProductRepo;
            _imageManagementService = imageManagementService;
            _discountPriceCalculator = discountPriceCalculator;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        #region Queries

        public async Task<Pagination<ProductDto>> GetPageAsync(ProductQueryParams query, CancellationToken cancellationToken = default)
        {
            var page = await _productRepo.GetPageAsync(query, cancellationToken);

            // One extra query for the whole page, never one per product.
            var discountsByProductId = await GetDiscountsByProductIdAsync(
                page.Data.Select(p => p.Id),
                cancellationToken);

            return new Pagination<ProductDto>(
                page.PageNumber,
                page.PageSize,
                page.TotalCount,
                page.Data.Select(product => MapToDto(product, discountsByProductId)).ToList());
        }

        public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var product = await _productRepo.GetByIdOrThrowAsync(
                cancellationToken,
                new object[] { id },
                p => p.Category!,
                p => p.Photos!);

            var discountsByProductId = await GetDiscountsByProductIdAsync(
                new[] { product.Id },
                cancellationToken);

            return MapToDto(product, discountsByProductId);
        }


        public async Task<ProductCartInfoDto> GetProductForAddToCartAsync(int productId, int quantity, CancellationToken cancellationToken = default, bool enforceAvailableStock = true)
        {
            if (quantity <= 0)
            {
                throw new BadRequestException("Quantity must be greater than zero.");
            }

            TblProducts product = await _productRepo.GetByIdOrThrowAsync(
                cancellationToken,
                new object[] { productId },
                p => p.Category!,
                p => p.Photos!);

            if (enforceAvailableStock && quantity > product.Quantity)
            {
                throw new BadRequestException(
                    $"Insufficient stock for '{product.Name}'. Available: {product.Quantity}, requested: {quantity}.");
            }

            var discountsByProductId = await GetDiscountsByProductIdAsync(
                new[] { product.Id },
                cancellationToken);
            var discounts = discountsByProductId.GetValueOrDefault(product.Id);

            // The calculator returns null when no discount is currently active, in which case the
            // product keeps its base price and no price-expiry is attached to the cart line.
            var priceAfterDiscount = _discountPriceCalculator.CalculatePriceAfterDiscount(product.Price, discounts);

            ProductCartInfoDto res = new ProductCartInfoDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                productImgsUrl = product.Photos?
                    .Where(p => p.IsDeleted != true)
                    .Select(x => x.PhotoPath)
                    .ToList(),
                ProductDescription = product.Description,
                Qunatity = quantity,
                BasePrice = product.Price,
                FinalPrice = priceAfterDiscount ?? product.Price,
                PriceExpiresAt = priceAfterDiscount.HasValue
                    ? discounts?.Max(d => d.EndDate)
                    : null,
                AvailableQuantity = product.Quantity
            };

            return res;
        }
        #endregion

        #region Commands

        public async Task<AddProductResult> AddAsync(
            AddProductDto product,
            string systemUserId,
            CancellationToken cancellationToken = default)
        {
            var storedPaths = await _imageManagementService
                .AddImagesAsync(product.Files, StaticEnums.product.ToString(), cancellationToken);

            try
            {
                var entity = _mapper.Map<TblProducts>(product);
                entity.Photos = storedPaths
                    .Select(path => new TblPhoto { PhotoPath = path, Product = entity, CreatedBy = systemUserId })
                    .ToList();

                var added = await _productRepo.AddAsync(entity, cancellationToken);

                var dto = _mapper.Map<ProductDto>(added);
                dto.CategoryName = await GetCategoryNameAsync(added.CategoryId, cancellationToken) ?? dto.CategoryName;

                return new AddProductResult(dto, storedPaths, entity);
            }
            catch
            {
                await RemoveStoredImagesAsync(storedPaths, cancellationToken);
                throw;
            }
        }

        public async Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default)
        {//Roll back
            await RemoveStoredImagesAsync(result?.StoredPhotoPaths, cancellationToken);
        }

        public async Task<ProductDto> UpdateAsync(
            UpdateProductDto product,
            string systemUserId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _productRepo.GetByIdOrThrowAsync(
                cancellationToken,
                new object[] { product.Id });

            _mapper.Map(product, entity);

            entity.UpdatedBy = systemUserId;

            var storedPaths = await _imageManagementService
                .AddImagesAsync(product.NewPhotos, StaticEnums.product.ToString(), cancellationToken);

            await ReconcilePhotosAsync(product.ExistingPhotoIds, storedPaths, product.Id, systemUserId, cancellationToken);

            var dto = _mapper.Map<ProductDto>(entity);
            dto.CategoryName = await GetCategoryNameAsync(entity.CategoryId, cancellationToken) ?? dto.CategoryName;

            return dto;
        }

        public async Task DeleteAsync(int id, string systemUserId, CancellationToken cancellationToken = default)
        {
            await _productRepo.DeleteOrThrowAsync(cancellationToken, id);

            var photos = await _photoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.ProductId == id && photo.IsDeleted == false,
                cancellationToken: cancellationToken);

            foreach (var photo in photos)
            {
                await _imageManagementService.SoftDeleteImageAsync(photo.Id, photo.PhotoPath, cancellationToken);
                photo.DeletedBy = systemUserId;
            }
        }


        #endregion

        #region Private Helpers

        private ProductDto MapToDto(TblProducts product, IReadOnlyDictionary<int, List<TblDiscount>> discountsByProductId)
        {
            ProductDto? dto = _mapper.Map<ProductDto>(product);

            discountsByProductId.TryGetValue(product.Id, out var discounts);

            // RealPrice is mapped from the entity by ProductMapping; PriceAfterDiscount is resolved here
            // because it depends on the discounts linked to the product, which the profile must not know about.
            dto.PriceAfterDiscount = _discountPriceCalculator.CalculatePriceAfterDiscount(dto.RealPrice, discounts);

            return dto;
        }

        private async Task<IReadOnlyDictionary<int, List<TblDiscount>>> GetDiscountsByProductIdAsync(IEnumerable<int> productIds, CancellationToken cancellationToken)
        {
            var links = await _discountProductRepo.GetByProductIdsAsync(productIds, cancellationToken);

            return links
                .Where(link => link.Discount != null)
                .GroupBy(link => link.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(link => link.Discount).ToList());
        }

        private async Task ReconcilePhotosAsync(List<int>? keptPhotoIds, IReadOnlyList<string> newPhotoPaths, int productId, string systemUserId, CancellationToken cancellationToken)
        {
            var kept = keptPhotoIds ?? new List<int>();

            var removed = await _photoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.ProductId == productId
                                    && photo.IsDeleted == false
                                    && !kept.Contains(photo.Id),
                trackChanges: true,
                cancellationToken: cancellationToken);

            foreach (var photo in removed)
            {
                photo.IsDeleted = true;
                photo.DeletedAt = DateTime.UtcNow;
                photo.DeletedBy = systemUserId;
            }

            if (newPhotoPaths.Count == 0)
            {
                return;
            }

            var newPhotos = newPhotoPaths
                .Select(path => new TblPhoto
                {
                    PhotoPath = path,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = systemUserId,
                    ProductId = productId,
                    CategoryId = null
                })
                .ToList();

            await _photoRepo.AddRangeAsync(newPhotos, cancellationToken);
        }

        private async Task<string?> GetCategoryNameAsync(int categoryId, CancellationToken cancellationToken)
        {
            var category = await _categoryRepo.GetByIdAsync(cancellationToken, categoryId);
            return category?.Name;
        }

        private async Task RemoveStoredImagesAsync(IReadOnlyList<string>? storedPaths, CancellationToken cancellationToken = default)
        {
            if (storedPaths is null || storedPaths.Count == 0)
            {
                return;
            }

            await _imageManagementService.RemoveImagesAsync(storedPaths, cancellationToken);
        }

        #endregion

        #endregion
    }
}
