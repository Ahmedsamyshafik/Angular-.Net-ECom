using Ecom.Core.DTO.Categories;
using Ecom.Core.DTO.Products;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;
using ECom.Api.Controllers.Category.V1;
using ECom.Api.DTO;
using ECom.Api.Helper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECom.Api.Tests;

public class CategoryControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ICategoryService> _categoryService;
    private readonly Mock<IValidator<AddCategoryFormDto>> _addCategoryValidator;
    private readonly Mock<IValidator<UpdateCategoryFormDto>> _updateCategoryValidator;
    private readonly Mock<IValidator<CategoryQueryParams>> _queryValidator;
    private readonly CategoryController _controller;

    public CategoryControllerTests()
    {
        _categoryService = new Mock<ICategoryService>();

        _addCategoryValidator = new Mock<IValidator<AddCategoryFormDto>>();
        _addCategoryValidator.Setup(v => v.ValidateAsync(It.IsAny<AddCategoryFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateCategoryValidator = new Mock<IValidator<UpdateCategoryFormDto>>();
        _updateCategoryValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateCategoryFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _queryValidator = new Mock<IValidator<CategoryQueryParams>>();
        _queryValidator.Setup(v => v.ValidateAsync(It.IsAny<CategoryQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.CategoryService).Returns(_categoryService.Object);

        _controller = new CategoryController(
            _unitOfWork.Object,
            _addCategoryValidator.Object,
            _updateCategoryValidator.Object,
            _queryValidator.Object);
    }

    private static CategoryDto SampleDto()
    {
        return new CategoryDto
        {
            Id = 1,
            Name = "Electronics",
            Description = "Devices",
            RowVersion = new byte[] { 1, 2 },
            Photos = new List<PhotoUrlWithId>()
        };
    }

    private static AddCategoryResult SampleAddResult(params string[] storedPaths)
    {
        return new AddCategoryResult(SampleDto(), storedPaths.ToList());
    }

    [Fact]
    public async Task GetAll_ShouldDelegateToCategoryServiceAndReturnEnvelope()
    {
        var page = new Pagination<CategoryDto>(1, 10, 1, new List<CategoryDto> { SampleDto() });
        _categoryService.Setup(s => s.GetPageAsync(
                It.IsAny<CategoryQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await _controller.GetAll(new CategoryQueryParams());

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        var pagination = Assert.IsType<Pagination<CategoryDto>>(api.Data);
        Assert.Equal(1, pagination.TotalCount);
        Assert.Equal(1, pagination.TotalPages);
        Assert.Same(page, pagination);
    }

    [Fact]
    public async Task GetAll_WhenQueryInvalid_ShouldThrowBeforeReachingTheService()
    {
        _queryValidator.Setup(v => v.ValidateAsync(It.IsAny<CategoryQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(CategoryQueryParams.PageNumber), "Page number must be greater than 0.")
            }));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.GetAll(new CategoryQueryParams()));
        _categoryService.Verify(
            s => s.GetPageAsync(It.IsAny<CategoryQueryParams>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_ShouldDelegateToCategoryService()
    {
        _categoryService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        var result = await _controller.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        Assert.Equal(SampleDto().Id, ((CategoryDto)api.Data!).Id);
    }

    [Fact]
    public async Task GetById_WhenCategoryMissing_ShouldPropagateNotFound()
    {
        _categoryService.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetById(999));
    }

    [Fact]
    public async Task Add_ShouldDelegateToCategoryServiceAndCommitOnce()
    {
        var form = new AddCategoryFormDto { Name = "Electronics", Description = "Devices" };

        var addResult = SampleAddResult("imgs/category/x.png");
        _categoryService.Setup(s => s.AddAsync(It.IsAny<AddCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addResult);

        var result = await _controller.Add(form);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);
        Assert.Same(addResult.Category, api.Data);

        _categoryService.Verify(
            s => s.AddAsync(
                It.Is<AddCategoryDto>(c => c.Name == "Electronics" && c.Description == "Devices"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Add_WhenCommitSucceeds_ShouldNotCompensate()
    {
        _categoryService.Setup(s => s.AddAsync(It.IsAny<AddCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleAddResult("imgs/category/x.png"));

        await _controller.Add(new AddCategoryFormDto { Name = "Electronics", Description = "Devices" });

        _unitOfWork.Verify(u => u.Commit(), Times.Once);
        _categoryService.Verify(
            s => s.CompensateAsync(It.IsAny<AddCategoryResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Add_WhenCommitFails_ShouldCompensateAndPropagate()
    {
        var addResult = SampleAddResult("imgs/category/x.png", "imgs/category/y.png");

        _categoryService.Setup(s => s.AddAsync(It.IsAny<AddCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addResult);

        _unitOfWork.Setup(u => u.Commit())
            .ThrowsAsync(new DbUpdateException("Database commit failed."));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => _controller.Add(new AddCategoryFormDto { Name = "Electronics", Description = "Devices" }));

        Assert.Equal("Database commit failed.", exception.Message);
        _categoryService.Verify(
            s => s.CompensateAsync(addResult, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Add_WhenServiceFails_ShouldNotCommit()
    {
        _categoryService.Setup(s => s.AddAsync(It.IsAny<AddCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Category is invalid."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Add(new AddCategoryFormDto { Name = "Electronics", Description = "Devices" }));

        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task Add_WhenFormInvalid_ShouldThrowBeforeReachingTheService()
    {
        _addCategoryValidator.Setup(v => v.ValidateAsync(It.IsAny<AddCategoryFormDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(AddCategoryFormDto.Name), "Name is required.")
            }));

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.Add(new AddCategoryFormDto()));
        _categoryService.Verify(
            s => s.AddAsync(It.IsAny<AddCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_ShouldDelegateToCategoryServiceAndCommitOnce()
    {
        var form = new UpdateCategoryFormDto
        {
            Id = 1,
            Name = "New Electronics",
            Description = "Updated",
            RowVersion = Convert.ToBase64String(new byte[] { 7, 6 })
        };

        _categoryService.Setup(s => s.UpdateAsync(It.IsAny<UpdateCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        var result = await _controller.Update(form);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<responseApi>(ok.Value);
        Assert.Equal(200, api.StatusCode);

        _categoryService.Verify(
            s => s.UpdateAsync(
                It.Is<UpdateCategoryDto>(c => c.Id == 1 && c.Name == "New Electronics" && c.RowVersion != null),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenCategoryMissing_ShouldPropagateNotFound()
    {
        _categoryService.Setup(s => s.UpdateAsync(It.IsAny<UpdateCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        var form = new UpdateCategoryFormDto { Id = 99, Name = "X", Description = "Y" };

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.Update(form));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Fact]
    public async Task Update_WhenConcurrencyConflict_ShouldPropagateToGlobalHandler()
    {
        _categoryService.Setup(s => s.UpdateAsync(It.IsAny<UpdateCategoryDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDto());

        _unitOfWork.Setup(u => u.Commit())
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrent update detected."));

        var form = new UpdateCategoryFormDto
        {
            Id = 1,
            Name = "X",
            Description = "Y",
            RowVersion = Convert.ToBase64String(new byte[] { 3, 3 })
        };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _controller.Update(form));
    }

    [Fact]
    public async Task Delete_ShouldDelegateToCategoryServiceAndCommitOnce()
    {
        _categoryService.Setup(s => s.DeleteAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(1);

        Assert.IsType<OkObjectResult>(result);
        _categoryService.Verify(s => s.DeleteAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenCategoryMissing_ShouldPropagateNotFound()
    {
        _categoryService.Setup(s => s.DeleteAsync(999, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.Delete(999));
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }
}
