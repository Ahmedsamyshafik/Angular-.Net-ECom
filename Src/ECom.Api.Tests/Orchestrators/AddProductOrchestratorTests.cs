using Ecom.Core.DTO.Products;
using Ecom.Core.Exceptions;
using Ecom.Core.Services;
using Ecom.Core.Services.Facads;

using ECom.Infrastructure.Application.Orchestrators;
using Moq;

namespace ECom.Api.Tests.Orchestrators;

public class AddProductOrchestratorTests
{
    private readonly Mock<IProductService> _productService;
    private readonly Mock<IDiscountFacade> _discountFacade;
    private readonly AddProductOrchestrator _orchestrator;

    public AddProductOrchestratorTests()
    {
        _productService = new Mock<IProductService>();
        _discountFacade = new Mock<IDiscountFacade>();

        _orchestrator = new AddProductOrchestrator(_productService.Object, _discountFacade.Object);
    }

    private static AddProductResult SampleResult(int id = 5)
    {
        return new AddProductResult(
            new ProductDto { Id = id, Name = "Laptop", CategoryName = "Electronics" },
            new List<string> { "imgs/product/x.png" });
    }

    private static AddProductDto MakeDto(AddDiscountDto discount = null)
    {
        return new AddProductDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 1200m,
            CategoryId = 3,
            Discount = discount
        };
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDiscount_ShouldOnlyRunTheProductStep()
    {
        _productService.Setup(s => s.AddAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleResult());

        var result = await _orchestrator.ExecuteAsync(MakeDto(), "1");

        Assert.Equal(5, result.Product.Id);
        _discountFacade.Verify(
            f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithDiscount_ShouldApplyItToTheGeneratedProductId()
    {
        _productService.Setup(s => s.AddAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleResult(7));

        var discount = new AddDiscountDto
        {
            From = new DateTime(2026, 1, 1),
            To = new DateTime(2026, 2, 1),
            Value = 10m
        };

        await _orchestrator.ExecuteAsync(MakeDto(discount), "1");

        _discountFacade.Verify(
            f => f.ApplyDiscountAsync(
                It.Is<AddDiscountDto>(d => d.ProductIds != null && d.ProductIds.Count == 1 && d.ProductIds.Contains(7)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunTheProductStepBeforeTheDiscountStep()
    {
        var order = new List<string>();

        _productService.Setup(s => s.AddAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("product"))
            .ReturnsAsync(SampleResult());

        _discountFacade.Setup(f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("discount"))
            .Returns(Task.CompletedTask);

        await _orchestrator.ExecuteAsync(MakeDto(new AddDiscountDto { Value = 5m }), "1");

        Assert.Equal(new[] { "product", "discount" }, order);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountFails_ShouldCompensateThenRethrow()
    {
        var result = SampleResult();

        _productService.Setup(s => s.AddAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _discountFacade.Setup(f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Provide at least one product id."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _orchestrator.ExecuteAsync(MakeDto(new AddDiscountDto { Value = 5m }), "1"));

        _productService.Verify(
            s => s.CompensateAsync(result, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProductStepFails_ShouldNotApplyDiscountOrCompensateAgain()
    {
        _productService.Setup(s => s.AddAsync(It.IsAny<AddProductDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Category is invalid."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _orchestrator.ExecuteAsync(MakeDto(new AddDiscountDto { Value = 5m }), "1"));

        _discountFacade.Verify(
            f => f.ApplyDiscountAsync(It.IsAny<AddDiscountDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _productService.Verify(
            s => s.CompensateAsync(It.IsAny<AddProductResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompensateAsync_ShouldDelegateToTheProductService()
    {
        var result = SampleResult();

        await _orchestrator.CompensateAsync(result);

        _productService.Verify(
            s => s.CompensateAsync(result, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
