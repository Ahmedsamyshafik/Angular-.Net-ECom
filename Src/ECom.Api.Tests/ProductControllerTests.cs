using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;
using Ecom.Core.Services.Facads;
using Ecom.Core.Services.Orchestrators;

using ECom.Api.Controllers.Product.V1;
using ECom.Api.DTO;
using ECom.Api.Helper;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Moq;

namespace ECom.Api.Tests;

public class ProductControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IProductService> _productService;
    private readonly Mock<IAddProductOrchestrator> _addProduct;
    private readonly Mock<IDiscountFacade> _discountFacade;
    private readonly Mock<IValidator<AddProductFormDto>> _productValidator;
    private readonly Mock<IValidator<UpdateProductFormDto>> _updateProductValidator;
    private readonly Mock<IValidator<AddDiscountDto>> _discountValidator;
    private readonly Mock<IValidator<ProductQueryParams>> _queryValidator;
    private readonly ProductController _controller;

    public ProductControllerTests()
    {
        _productService = new Mock<IProductService>();
        _addProduct = new Mock<IAddProductOrchestrator>();
        _discountFacade = new Mock<IDiscountFacade>();

        _productValidator = new Mock<IValidator<AddProductFormDto>>();
        _productValidator.Setup(v => v.ValidateAsync(It.IsAny<AddProductFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateProductValidator = new Mock<IValidator<UpdateProductFormDto>>();
        _updateProductValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateProductFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _discountValidator = new Mock<IValidator<AddDiscountDto>>();
        _discountValidator.Setup(v => v.ValidateAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _queryValidator = new Mock<IValidator<ProductQueryParams>>();
        _queryValidator.Setup(v => v.ValidateAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ProductService).Returns(_productService.Object);
        _unitOfWork.Setup(u => u.AddProductOrchestrator).Returns(_addProduct.Object);
        _unitOfWork.Setup(u => u.DiscountFacade).Returns(_discountFacade.Object);

        _controller = new ProductController(
            _unitOfWork.Object,
            _productValidator.Object,
            _updateProductValidator.Object,
            _discountValidator.Object,
            _queryValidator.Object);
    }

    private static ProductDto SampleDto()
    {
        return new ProductDto
        {
            Id = 1,
            Name = "Laptop",
            RealPrice = 999.99m,
            PriceAfterDiscount = 799.99m,
            CategoryName = "Electronics",
            RowVersion = new byte[] { 1, 2, 3 },
            PhotoUrls = new List<PhotoUrlWithId>()
        };
    }

    private static AddProductResult SampleAddResult(params string[] storedPaths)
    {
        return new AddProductResult(SampleDto(), storedPaths.ToList());
    }

    [Fact]
    public async Task GetAll_ShouldDelegateToProductServiceAndReturnEnvelope()
    {
        var page = new Pagination<ProductDto>(1, 10, 1, new List<ProductDto> { SampleDto() });
        _productService.Setup(s => s.GetPageAsync(
                It.IsAny<ProductQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await _controller.GetAllV1(new ProductQueryParams());

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        var pagination = Assert.IsType<Pagination<ProductDto>>(api.Data);
        Assert.Equal(1, pagination.TotalCount);
        Assert.Equal(1, pagination.TotalPages);
        Assert.Same(page, pagination);
    }

    [Fact]
    public async Task GetAll_WhenQueryInvalid_ShouldThrowBeforeReachingTheService()
    {
        _queryValidator.Setup(v => v.ValidateAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(ProductQueryParams.PageNumber), "Page number must be greater than 0.")
            }));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.GetAllV1(new ProductQueryParams()));
        _productService.Verify(
            s => s.GetPageAsync(It.IsAny<ProductQueryParams>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_ShouldDelegateToProductService()
    {
        _productService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        var result = await _controller.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        Assert.Equal(SampleDto().Id, ((ProductDto)api.Data!).Id);
    }

    [Fact]
    public async Task GetById_WhenProductMissing_ShouldPropagateNotFound()
    {
        _productService.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetById(999));
    }

    [Fact]
    public async Task Add_ShouldDelegateToOrchestratorAndCommitOnce()
    {
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Files = new List<IFormFile>()
        };

        var addResult = SampleAddResult("imgs/product/x.png");
        _addProduct.Setup(o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addResult);

        var result = await _controller.Add(form);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        Assert.Same(addResult.Product, api.Data);

        _addProduct.Verify(
            o => o.ExecuteAsync(
                It.Is<AddProductDto>(d => d.Name == "Laptop" && d.CategoryId == 3 && d.Price == 1200m),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Add_WithDiscount_ShouldPassTheDiscountToTheOrchestrator()
    {
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            DiscountFrom = new DateTime(2026, 1, 1),
            DiscountTo = new DateTime(2026, 2, 1),
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10m,
            DiscountName = "New Year"
        };

        _addProduct.Setup(o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleAddResult());

        var result = await _controller.Add(form);

        Assert.IsType<OkObjectResult>(result);
        _addProduct.Verify(
            o => o.ExecuteAsync(
                It.Is<AddProductDto>(d => d.Discount != null && d.Discount.DiscountType == DiscountType.Percentage),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _discountFacade.Verify(
            f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Add_WhenCommitSucceeds_ShouldNotCompensate()
    {
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Files = new List<IFormFile>()
        };

        _addProduct.Setup(o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleAddResult("imgs/product/x.png"));

        await _controller.Add(form);

        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _addProduct.Verify(
            o => o.CompensateAsync(It.IsAny<AddProductResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Add_WhenCommitFails_ShouldCompensateAndPropagate()
    {
        var addResult = SampleAddResult("imgs/product/x.png", "imgs/product/y.png");

        _addProduct.Setup(o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addResult);

        _unitOfWork.Setup(u => u.Commit())
            .ThrowsAsync(new DbUpdateException("Database commit failed."));

        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Files = new List<IFormFile>()
        };

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _controller.Add(form));

        Assert.Equal("Database commit failed.", exception.Message);
        _addProduct.Verify(
            o => o.CompensateAsync(addResult, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Add_WhenOrchestratorFails_ShouldNotCommit()
    {
        _addProduct.Setup(o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Provide a valid category."));

        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Files = new List<IFormFile>()
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.Add(form));

        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task Add_WhenFormInvalid_ShouldThrowBeforeReachingTheOrchestrator()
    {
        _productValidator.Setup(v => v.ValidateAsync(It.IsAny<AddProductFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(AddProductFormDto.Name), "Name is required.")
            }));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.Add(new AddProductFormDto()));
        _addProduct.Verify(
            o => o.ExecuteAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_ShouldDelegateToProductServiceAndCommitOnce()
    {
        var form = new UpdateProductFormDto
        {
            Id = 1,
            Name = "Updated Laptop",
            Description = "Updated",
            Price = 1100m,
            CategoryId = 1,
            RowVersion = Convert.ToBase64String(new byte[] { 9, 8, 7 })
        };

        _productService.Setup(s => s.UpdateAsync(It.IsAny<UpdateProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        var result = await _controller.Update(form);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);

        _productService.Verify(
            s => s.UpdateAsync(
                It.Is<UpdateProductDto>(d => d.Id == 1 && d.Name == "Updated Laptop" && d.RowVersion != null),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenProductMissing_ShouldPropagateNotFound()
    {
        _productService.Setup(s => s.UpdateAsync(It.IsAny<UpdateProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        var form = new UpdateProductFormDto { Id = 99, Name = "X", Description = "Y", Price = 1m, CategoryId = 1 };

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.Update(form));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task Update_WhenConcurrencyConflict_ShouldPropagateToGlobalHandler()
    {
        _productService.Setup(s => s.UpdateAsync(It.IsAny<UpdateProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        _unitOfWork.Setup(u => u.Commit())
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrent update detected."));

        var form = new UpdateProductFormDto
        {
            Id = 1,
            Name = "Updated",
            Description = "Updated",
            Price = 1m,
            CategoryId = 1,
            RowVersion = Convert.ToBase64String(new byte[] { 5, 5, 5 })
        };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _controller.Update(form));
    }

    [Fact]
    public async Task Delete_ShouldDelegateToProductServiceAndCommitOnce()
    {
        _productService.Setup(s => s.DeleteAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(1);

        Assert.IsType<OkObjectResult>(result);
        _productService.Verify(s => s.DeleteAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenProductMissing_ShouldPropagateNotFound()
    {
        _productService.Setup(s => s.DeleteAsync(999, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.Delete(999));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task ApplyDiscount_WhenNoProductIds_ShouldThrowBadRequest()
    {
        var discount = new AddDiscountDto { ProductIds = new List<int>() };

        _discountValidator.Setup(v => v.ValidateAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(AddDiscountDto.ProductIds), "Provide at least one product id or a category id.")
            }));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.ApplyDiscount(discount));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task ApplyDiscount_HappyPath_ShouldDelegateToFacadeAndCommit()
    {
        var discount = new AddDiscountDto
        {
            ProductIds = new List<int> { 1, 2 },
            From = new DateTime(2026, 1, 1),
            To = new DateTime(2026, 2, 1),
            DiscountType = DiscountType.Percentage,
            Value = 10m
        };

        _discountFacade.Setup(f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ApplyDiscount(discount);

        Assert.IsType<OkObjectResult>(result);
        _discountFacade.Verify(
            f => f.ApplyDiscountAsync(
                It.Is<AddDiscountDto>(d => d.ProductIds != null && d.ProductIds.Contains(1) && d.ProductIds.Contains(2)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task ApplyCategoryDiscount_WhenNoCategoryId_ShouldThrowBadRequest()
    {
        var discount = new AddDiscountDto { CategoryId = null, ProductIds = new List<int> { 1 } };

        _discountFacade.Setup(f => f.ApplyCategoryDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Provide a category id."));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.ApplyCategoryDiscount(discount));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task ApplyCategoryDiscount_WhenNoProductsInCategory_ShouldThrowNotFound()
    {
        var discount = new AddDiscountDto { CategoryId = 9, ProductIds = new List<int> { 1 } };

        _discountFacade.Setup(f => f.ApplyCategoryDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No products found in this category."));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.ApplyCategoryDiscount(discount));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task ApplyCategoryDiscount_HappyPath_ShouldDelegateToFacadeAndCommit()
    {
        var discount = new AddDiscountDto { CategoryId = 3 };

        _discountFacade.Setup(f => f.ApplyCategoryDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ApplyCategoryDiscount(discount);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);

        _discountFacade.Verify(
            f => f.ApplyCategoryDiscountAsync(discount, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }
}
