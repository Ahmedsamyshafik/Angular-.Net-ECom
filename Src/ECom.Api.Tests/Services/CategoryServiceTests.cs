using Ecom.Core.DTO.Categories;
using Ecom.Core.DTO.Common;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;
using ECom.Api.Tests.Mapping;
using ECom.Infrastructure.Application.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECom.Api.Tests.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepo> _categoryRepo;
    private readonly Mock<IPhotoRepo> _photoRepo;
    private readonly Mock<IImageManagementService> _imageService;
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _categoryRepo = new Mock<ICategoryRepo>();
        _photoRepo = new Mock<IPhotoRepo>();
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

        _service = new CategoryService(
            _categoryRepo.Object,
            _photoRepo.Object,
            _imageService.Object,
            MappingTestHelper.CreateMapper());
    }

    private static TblCategories SampleCategory()
    {
        return new TblCategories
        {
            Id = 1,
            Name = "Electronics",
            Description = "Devices",
            RowVersion = new byte[] { 1, 2 },
            Photos = new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/category/a.png" },
                new TblPhoto { Id = 11, PhotoPath = "imgs/category/b.png", IsDeleted = true }
            }
        };
    }

    #region Queries

    [Fact]
    public async Task GetPageAsync_ShouldMapEntitiesToDtosAndKeepPaging()
    {
        var page = new Pagination<TblCategories>(1, 10, 1, new List<TblCategories> { SampleCategory() });
        _categoryRepo.Setup(r => r.GetPageAsync(It.IsAny<CategoryQueryParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await _service.GetPageAsync(new CategoryQueryParams());

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        var dto = Assert.Single(result.Data);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Electronics", dto.Name);
        Assert.Equal("Devices", dto.Description);
        Assert.Equal(new byte[] { 1, 2 }, dto.RowVersion);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMapOnlyActivePhotos()
    {
        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ReturnsAsync(SampleCategory());

        var dto = await _service.GetByIdAsync(1);

        var photo = Assert.Single(dto.Photos);
        Assert.Equal(10, photo.Id);
        Assert.Equal("imgs/category/a.png", photo.Url);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryMissing_ShouldThrowNotFound()
    {
        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
    }

    #endregion

    #region Add

    [Fact]
    public async Task AddAsync_ShouldStoreImagesStageCategoryAndReturnMappedResult()
    {
        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "category",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/category/x.png" });

        TblCategories staged = null;
        _categoryRepo.Setup(r => r.AddAsync(It.IsAny<TblCategories>(), It.IsAny<CancellationToken>()))
            .Callback<TblCategories, CancellationToken>((c, _) => staged = c)
            .ReturnsAsync((TblCategories c, CancellationToken _) => c);

        var result = await _service.AddAsync(new AddCategoryDto
        {
            Name = "Electronics",
            Description = "Devices",
            Files = new List<FileParameterDto>()
        });

        Assert.Equal("Electronics", staged!.Name);
        Assert.Equal("Devices", staged.Description);
        var photo = Assert.Single(staged.Photos!);
        Assert.Equal("imgs/category/x.png", photo.PhotoPath);
        Assert.Same(staged, photo.Category);

        Assert.Equal("Electronics", result.Category.Name);
        Assert.Equal(new[] { "imgs/category/x.png" }, result.StoredPhotoPaths);
    }

    [Fact]
    public async Task AddAsync_WhenStagingFails_ShouldRemoveTheStoredFiles()
    {
        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "category",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/category/x.png" });

        _categoryRepo.Setup(r => r.AddAsync(It.IsAny<TblCategories>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("staging failed"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.AddAsync(new AddCategoryDto
        {
            Name = "Electronics",
            Description = "Devices"
        }));

        _imageService.Verify(
            s => s.RemoveImagesAsync(It.Is<IEnumerable<string>>(p => p.Contains("imgs/category/x.png"))),
            Times.Once);
    }

    [Fact]
    public async Task CompensateAsync_ShouldRemoveTheStoredFiles()
    {
        var result = new AddCategoryResult(
            new CategoryDto { Id = 1, Name = "Electronics", Description = "Devices" },
            new List<string> { "imgs/category/1.png" });

        await _service.CompensateAsync(result);

        _imageService.Verify(
            s => s.RemoveImagesAsync(It.Is<IEnumerable<string>>(p => p.Contains("imgs/category/1.png"))),
            Times.Once);
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateAsync_ShouldApplyFieldsStampUpdatedAtAndAttachClientRowVersion()
    {
        var category = SampleCategory();
        category.UpdatedAt = null;

        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ReturnsAsync(category);

        var clientRowVersion = new byte[] { 7, 6 };

        await _service.UpdateAsync(new UpdateCategoryDto
        {
            Id = 1,
            Name = "New Electronics",
            Description = "Updated",
            RowVersion = clientRowVersion
        });

        Assert.Equal("New Electronics", category.Name);
        Assert.Equal("Updated", category.Description);
        Assert.NotNull(category.UpdatedAt);
        Assert.Equal(clientRowVersion, category.RowVersion);
    }

    [Fact]
    public async Task UpdateAsync_WithNullRowVersion_ShouldPreserveEntityRowVersion()
    {
        var category = SampleCategory();
        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ReturnsAsync(category);

        await _service.UpdateAsync(new UpdateCategoryDto
        {
            Id = 1,
            Name = "New Electronics",
            Description = "Updated",
            RowVersion = null
        });

        Assert.Equal(new byte[] { 1, 2 }, category.RowVersion);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSoftDeleteRemovedPhotosAndStageNewOnes()
    {
        var category = SampleCategory();
        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ReturnsAsync(category);

        _imageService.Setup(s => s.AddImagesAsync(
                It.IsAny<List<FileParameterDto>>(),
                "category",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "imgs/category/new.png" });

        List<TblPhoto> removedPhotos = null;
        _photoRepo.Setup(p => p.GetAllWithDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>()))
            .ReturnsAsync(() => removedPhotos = new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/category/a.png", CategoryId = 1 }
            });

        List<TblPhoto> addedPhotos = null;
        _photoRepo.Setup(p => p.AddRangeAsync(It.IsAny<List<TblPhoto>>(), It.IsAny<CancellationToken>()))
            .Callback<List<TblPhoto>, CancellationToken>((list, _) => addedPhotos = list)
            .ReturnsAsync((List<TblPhoto> list, CancellationToken _) => list);

        await _service.UpdateAsync(new UpdateCategoryDto
        {
            Id = 1,
            Name = "New Electronics",
            Description = "Updated",
            ExistingPhotoIds = new List<int>(),
            NewPhotos = new List<FileParameterDto>()
        });

        var removed = Assert.Single(removedPhotos!);
        Assert.True(removed.IsDeleted);
        Assert.NotNull(removed.DeletedAt);

        var added = Assert.Single(addedPhotos!);
        Assert.Equal("imgs/category/new.png", added.PhotoPath);
        Assert.Equal(1, added.CategoryId);
        Assert.Null(added.ProductId);
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryMissing_ShouldThrowNotFound()
    {
        _categoryRepo.Setup(r => r.GetByIdOrThrowAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblCategories, object>>[]>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(new UpdateCategoryDto
        {
            Id = 99,
            Name = "X",
            Description = "Y"
        }));
    }

    #endregion

    #region Delete

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteCategoryAndItsActivePhotos()
    {
        _categoryRepo.Setup(r => r.DeleteOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .Returns(Task.CompletedTask);

        _photoRepo.Setup(p => p.GetAllWithDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TblPhoto, object>>[]>()))
            .ReturnsAsync(new List<TblPhoto>
            {
                new TblPhoto { Id = 10, PhotoPath = "imgs/category/a.png", CategoryId = 1 }
            });

        await _service.DeleteAsync(1);

        _imageService.Verify(s => s.SoftDeleteImageAsync(10, "imgs/category/a.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenCategoryMissing_ShouldThrowNotFound()
    {
        _categoryRepo.Setup(r => r.DeleteOrThrowAsync(It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .ThrowsAsync(new NotFoundException("Category not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
    }

    #endregion
}
