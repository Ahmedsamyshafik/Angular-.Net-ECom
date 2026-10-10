using Ecom.Core.DTO.Common;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;
using ECom.Api.Tests.Mapping;
using ECom.Infrastructure.Application.Calculators;
using ECom.Infrastructure.Application.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECom.Api.Tests.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepo> _productRepo;
    private readonly Mock<ICategoryRepo> _categoryRepo;
    private readonly Mock<IPhotoRepo> _photoRepo;
    private readonly Mock<IDiscountProductRepo> _discountProductRepo;
    private readonly Mock<IImageManagementService> _imageService;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _productRepo = new Mock<IProductRepo>();
        _categoryRepo = new Mock<ICategoryRepo>();
        _photoRepo = new Mock<IPhotoRepo>();
        _discountProductRepo = new Mock<IDiscountProductRepo>();
        _imageService = new Mock<IImageManagementService>();

        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _photoRepo.Setup(p => p.GetAllWithDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>()))
            .ReturnsAsync(new List<TblPhoto>());

        _discountProductRepo.Setup(r => r.GetByProductIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TblProductDiscount>());

        _service = new ProductService(
            _productRepo.Object,
            _categoryRepo.Object,
            _photoRepo.Object,
            _discountProductRepo.Object,
            _imageService.Object,
            new DiscountPriceCalculator(),
            MappingTestHelper.CreateMapper());
    }

    private static TblProducts SampleProduct()
    {
        return new TblProducts
        {
            Id = 1,
            Name = "Laptop",
            Description = "A laptop",
            Price = 999.99m,
            CategoryId = 3,
            RowVersion = new byte[] { 1, 2, 3 },
            Category = new TblCategories { Id = 3, Name = "Electronics" },
            Photos = new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/product/a.png" },
                new TblPhoto { Id = 11, PhotoPath = "imgs/product/b.png", IsDeleted = true }
            }
        };
    }

    private void SetupDiscountFor(
        int productId,
        DiscountType type,
        decimal value,
        DateTime? from = null,
        DateTime? to = null)
    {
        var discount = new TblDiscount
        {
            Id = 1,
            Type = type,
            Value = value,
            StartDate = from ?? DateTime.UtcNow.AddDays(-1),
            EndDate = to ?? DateTime.UtcNow.AddDays(1),
            IsActive = true
        };

        _discountProductRepo.Setup(r => r.GetByProductIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TblProductDiscount>
            {
                new TblProductDiscount { Id = 1, ProductId = productId, Discount = discount }
            });
    }

    #region Queries

    [Fact]
    public async Task GetPageAsync_ShouldMapEntitiesToDtosAndKeepPaging()
    {
        var product = SampleProduct();
        var page = new Pagination<TblProducts>(2, 5, 11, new List<TblProducts> { product });
        _productRepo.Setup(r => r.GetPageAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await _service.GetPageAsync(new ProductQueryParams());

        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(11, result.TotalCount);
        var dto = Assert.Single(result.Data);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Laptop", dto.Name);
        Assert.Equal(999.99m, dto.RealPrice);
        Assert.Null(dto.PriceAfterDiscount);
        Assert.Equal("Electronics", dto.CategoryName);
        Assert.Equal(new byte[] { 1, 2, 3 }, dto.RowVersion);
    }

    [Fact]
    public async Task GetPageAsync_WhenProductHasActiveDiscount_ShouldSetPriceAfterDiscount()
    {
        var product = SampleProduct();
        product.Price = 1000m;
        _productRepo.Setup(r => r.GetPageAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pagination<TblProducts>(1, 10, 1, new List<TblProducts> { product }));

        SetupDiscountFor(1, DiscountType.Percentage, 20m);

        var result = await _service.GetPageAsync(new ProductQueryParams());

        var dto = Assert.Single(result.Data);
        Assert.Equal(1000m, dto.RealPrice);
        Assert.Equal(800m, dto.PriceAfterDiscount);
    }

    [Fact]
    public async Task GetPageAsync_WhenProductHasNoDiscount_ShouldLeavePriceAfterDiscountNull()
    {
        var product = SampleProduct();
        product.Price = 1000m;
        _productRepo.Setup(r => r.GetPageAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pagination<TblProducts>(1, 10, 1, new List<TblProducts> { product }));

        var result = await _service.GetPageAsync(new ProductQueryParams());

        var dto = Assert.Single(result.Data);
        Assert.Equal(1000m, dto.RealPrice);
        Assert.Null(dto.PriceAfterDiscount);
    }

    [Fact]
    public async Task GetPageAsync_ShouldRequestDiscountsForTheWholePageInASingleCall()
    {
        var page = new Pagination<TblProducts>(
            1, 10, 2, new List<TblProducts>
            {
                new TblProducts { Id = 1, Name = "A", Price = 10m },
                new TblProducts { Id = 2, Name = "B", Price = 20m }
            });

        _productRepo.Setup(r => r.GetPageAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        await _service.GetPageAsync(new ProductQueryParams());

        // One lookup for the page, not one per product.
        _discountProductRepo.Verify(r => r.GetByProductIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMapEntityIncludingCategoryAndActivePhotos()
    {
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblProducts, object>>[]>()))
            .ReturnsAsync(SampleProduct());

        var dto = await _service.GetByIdAsync(1);

        Assert.Equal("Electronics", dto.CategoryName);
        var photo = Assert.Single(dto.PhotoUrls);
        Assert.Equal(10, photo.Id);
        Assert.Equal("imgs/product/a.png", photo.Url);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductMissing_ShouldThrowNotFound()
    {
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblProducts, object>>[]>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRealPriceAndPriceAfterDiscount()
    {
        var product = SampleProduct();
        product.Price = 1000m;

        _productRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblProducts, object>>[]>()))
            .ReturnsAsync(product);

        SetupDiscountFor(1, DiscountType.Percentage, 20m);

        var dto = await _service.GetByIdAsync(1);

        Assert.Equal(1000m, dto.RealPrice);
        Assert.Equal(800m, dto.PriceAfterDiscount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenDiscountExpired_ShouldLeavePriceAfterDiscountNull()
    {
        var product = SampleProduct();
        product.Price = 1000m;

        _productRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblProducts, object>>[]>()))
            .ReturnsAsync(product);

        SetupDiscountFor(
            1,
            DiscountType.Percentage,
            20m,
            from: DateTime.UtcNow.AddDays(-10),
            to: DateTime.UtcNow.AddDays(-1));

        var dto = await _service.GetByIdAsync(1);

        Assert.Equal(1000m, dto.RealPrice);
        Assert.Null(dto.PriceAfterDiscount);
    }

    #endregion

    #region Add

    [Fact]
    public async Task AddAsync_ShouldStoreImagesStageProductAndReturnMappedResult()
    {
        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "product",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/product/x.png" });

        TblProducts staged = null;
        _productRepo.Setup(r => r.AddAsync(It.IsAny<TblProducts>(), It.IsAny<CancellationToken>()))
            .Callback<TblProducts, CancellationToken>((p, _) => staged = p)
            .ReturnsAsync((TblProducts p, CancellationToken _) => p);

        _categoryRepo.Setup(c => c.GetByIdAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ReturnsAsync(new TblCategories { Id = 3, Name = "Electronics" });

        var result = await _service.AddAsync(new AddProductDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Files = new List<FileParameterDto>()
        });

        Assert.Equal("Laptop", staged!.Name);
        Assert.Equal(1200m, staged.Price);
        Assert.Equal(3, staged.CategoryId);
        var photo = Assert.Single(staged.Photos!);
        Assert.Equal("imgs/product/x.png", photo.PhotoPath);
        Assert.Same(staged, photo.Product);

        Assert.Equal("Laptop", result.Product.Name);
        Assert.Equal("Electronics", result.Product.CategoryName);
        Assert.Equal(new[] { "imgs/product/x.png" }, result.StoredPhotoPaths);
    }

    [Fact]
    public async Task AddAsync_WhenStagingFails_ShouldRemoveTheStoredFiles()
    {
        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "product",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/product/x.png" });

        _productRepo.Setup(r => r.AddAsync(It.IsAny<TblProducts>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("staging failed"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.AddAsync(new AddProductDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3
        }));

        _imageService.Verify(
            s => s.RemoveImagesAsync(It.Is<IEnumerable<string>>(p => p.Contains("imgs/product/x.png"))),
            Times.Once);
    }

    [Fact]
    public async Task AddAsync_WhenNoFilesStored_ShouldNotAttemptCleanup()
    {
        _productRepo.Setup(r => r.AddAsync(It.IsAny<TblProducts>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("staging failed"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.AddAsync(new AddProductDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3
        }));

        _imageService.Verify(s => s.RemoveImagesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task CompensateAsync_ShouldRemoveTheStoredFiles()
    {
        var result = new AddProductResult(
            new ProductDto { Id = 1, Name = "Laptop", CategoryName = "Electronics" },
            new List<string> { "imgs/product/1.png", "imgs/product/2.png" });

        await _service.CompensateAsync(result);

        _imageService.Verify(
            s => s.RemoveImagesAsync(It.Is<IEnumerable<string>>(p => p.Count() == 2)),
            Times.Once);
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateAsync_WithRowVersion_ShouldApplyFieldsAndAttachClientRowVersion()
    {
        var product = SampleProduct();
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ReturnsAsync(product);

        _categoryRepo.Setup(c => c.GetByIdAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ReturnsAsync(new TblCategories { Id = 4, Name = "Computers" });

        var clientRowVersion = new byte[] { 9, 8, 7 };

        var dto = await _service.UpdateAsync(new UpdateProductDto
        {
            Id = 1,
            Name = "Updated Laptop",
            Description = "Updated",
            Price = 1100m,
            CategoryId = 4,
            RowVersion = clientRowVersion
        });

        Assert.Equal("Updated Laptop", product.Name);
        Assert.Equal("Updated", product.Description);
        Assert.Equal(1100m, product.Price);
        Assert.Equal(4, product.CategoryId);
        Assert.Equal(clientRowVersion, product.RowVersion);
        Assert.Equal("Computers", dto.CategoryName);
    }

    [Fact]
    public async Task UpdateAsync_WithNullRowVersion_ShouldPreserveEntityRowVersion()
    {
        var product = SampleProduct();
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ReturnsAsync(product);

        await _service.UpdateAsync(new UpdateProductDto
        {
            Id = 1,
            Name = "Updated",
            Description = "Updated",
            Price = 1m,
            CategoryId = 1,
            RowVersion = null
        });

        Assert.Equal(new byte[] { 1, 2, 3 }, product.RowVersion);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSoftDeleteRemovedPhotosAndStageNewOnes()
    {
        var product = SampleProduct();
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ReturnsAsync(product);

        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "product",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/product/new.png" });

        List<TblPhoto> removedPhotos = null;
        _photoRepo.Setup(p => p.GetAllWithDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>()))
            .Callback<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>, bool, CancellationToken,
                System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>(
                (_, _, _, _) => { })
            .ReturnsAsync(() => removedPhotos = new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/product/a.png", ProductId = 1 }
            });

        List<TblPhoto> addedPhotos = null;
        _photoRepo.Setup(p => p.AddRangeAsync(It.IsAny<List<TblPhoto>>(), It.IsAny<CancellationToken>()))
            .Callback<List<TblPhoto>, CancellationToken>((list, _) => addedPhotos = list)
            .ReturnsAsync((List<TblPhoto> list, CancellationToken _) => list);

        await _service.UpdateAsync(new UpdateProductDto
        {
            Id = 1,
            Name = "Updated",
            Description = "Updated",
            Price = 1m,
            CategoryId = 1,
            ExistingPhotoIds = new List<int>(),
            NewPhotos = new List<FileParameterDto>()
        });

        var removed = Assert.Single(removedPhotos!);
        Assert.True(removed.IsDeleted);
        Assert.NotNull(removed.DeletedAt);

        var added = Assert.Single(addedPhotos!);
        Assert.Equal("imgs/product/new.png", added.PhotoPath);
        Assert.Equal(1, added.ProductId);
        Assert.Null(added.CategoryId);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductMissing_ShouldThrowNotFound()
    {
        _productRepo.Setup(r => r.GetByIdOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(new UpdateProductDto
        {
            Id = 99,
            Name = "X",
            Description = "Y",
            Price = 1m,
            CategoryId = 1
        }));
    }

    #endregion

    #region Delete

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProductAndItsActivePhotos()
    {
        _productRepo.Setup(r => r.DeleteOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .Returns(Task.CompletedTask);

        _photoRepo.Setup(p => p.GetAllWithDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>()))
            .ReturnsAsync(new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/product/a.png", ProductId = 1 }
            });

        await _service.DeleteAsync(1);

        _imageService.Verify(s => s.SoftDeleteImageAsync(10, "imgs/product/a.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenProductMissing_ShouldThrowNotFound()
    {
        _productRepo.Setup(r => r.DeleteOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
        _imageService.Verify(
            s => s.SoftDeleteImageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
