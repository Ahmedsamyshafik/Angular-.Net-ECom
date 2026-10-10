using Ecom.Core.DTO.Categories;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Discount;
using ECom.Api.DTO;
using Microsoft.AspNetCore.Http;

namespace ECom.Api.Tests;

public class FormDtoExtensionsTests
{
    [Fact]
    public void AddProductForm_WithDiscountPosted_ShouldPopulateDiscountDto()
    {
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 100m,
            CategoryId = 1,
            DiscountFrom = new DateTime(2026, 1, 1),
            DiscountTo = new DateTime(2026, 2, 1),
            DiscountType = DiscountType.Percentage,
            DiscountValue = 15m,
            DiscountName = "New Year"
        };

        var dto = form.ToDto();

        Assert.Equal("Laptop", dto.Name);
        Assert.Equal(100m, dto.Price);
        Assert.Equal(1, dto.CategoryId);
        Assert.NotNull(dto.Discount);
        Assert.Equal(new DateTime(2026, 1, 1), dto.Discount.From);
        Assert.Equal(new DateTime(2026, 2, 1), dto.Discount.To);
        Assert.Equal(DiscountType.Percentage, dto.Discount.DiscountType);
        Assert.Equal(15m, dto.Discount.Value);
        Assert.Equal("New Year", dto.Discount.Name);
    }

    [Fact]
    public void AddProductForm_WithoutDiscountInfo_ShouldLeaveDiscountNull()
    {
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 100m,
            CategoryId = 1
        };

        var dto = form.ToDto();

        Assert.Null(dto.Discount);
    }

    [Fact]
    public void AddProductForm_WithFiles_ShouldConvertToFileParameters()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var form = new AddProductFormDto
        {
            Name = "Laptop",
            Description = "A laptop",
            Price = 100m,
            CategoryId = 1,
            Files = new List<IFormFile>
            {
                new FormFile(new MemoryStream(bytes), 0, bytes.Length, "img", "img.png")
                {
                    Headers = new HeaderDictionary { { "Content-Type", "image/png" } }
                }
            }
        };

        var dto = form.ToDto();

        var file = Assert.Single(dto.Files!);
        Assert.Equal(bytes, file.Data);
        Assert.Equal("img.png", file.FileName);
        Assert.Equal("image/png", file.ContentType);
    }

    [Fact]
    public void UpdateProductForm_WithRowVersion_ShouldConvertFromBase64()
    {
        var rowVersion = new byte[] { 10, 20, 30 };
        var form = new UpdateProductFormDto
        {
            Id = 1,
            Name = "Laptop",
            Description = "A laptop",
            Price = 100m,
            CategoryId = 1,
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var dto = form.ToDto();

        Assert.Equal(rowVersion, dto.RowVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateProductForm_WithoutRowVersion_ShouldLeaveItNull(string? rowVersion)
    {
        var form = new UpdateProductFormDto
        {
            Id = 1,
            Name = "Laptop",
            Description = "A laptop",
            Price = 100m,
            CategoryId = 1,
            RowVersion = rowVersion
        };

        var dto = form.ToDto();

        Assert.Null(dto.RowVersion);
    }

    [Fact]
    public void UpdateCategoryForm_WithRowVersion_ShouldConvertFromBase64()
    {
        var rowVersion = new byte[] { 7, 8 };
        var form = new UpdateCategoryFormDto
        {
            Id = 1,
            Name = "Electronics",
            Description = "Devices",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var dto = form.ToDto();

        Assert.Equal(rowVersion, dto.RowVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateCategoryForm_WithoutRowVersion_ShouldLeaveItNull(string? rowVersion)
    {
        var form = new UpdateCategoryFormDto
        {
            Id = 1,
            Name = "Electronics",
            Description = "Devices",
            RowVersion = rowVersion
        };

        var dto = form.ToDto();

        Assert.Null(dto.RowVersion);
    }

    [Fact]
    public void AddCategoryForm_ShouldMapScalarFields()
    {
        var form = new AddCategoryFormDto
        {
            Name = "Electronics",
            Description = "Devices",
            Files = new List<IFormFile>()
        };

        var dto = form.ToDto();

        Assert.Equal("Electronics", dto.Name);
        Assert.Equal("Devices", dto.Description);
        Assert.NotNull(dto.Files);
        Assert.Empty(dto.Files);
    }

    [Fact]
    public void AddCategoryForm_WithFiles_ShouldConvertToFileParameters()
    {
        var bytes = new byte[] { 4, 5, 6 };
        var form = new AddCategoryFormDto
        {
            Name = "Electronics",
            Description = "Devices",
            Files = new List<IFormFile>
            {
                new FormFile(new MemoryStream(bytes), 0, bytes.Length, "img", "cat.png")
                {
                    Headers = new HeaderDictionary { { "Content-Type", "image/png" } }
                }
            }
        };

        var dto = form.ToDto();

        var file = Assert.Single(dto.Files!);
        Assert.Equal(bytes, file.Data);
        Assert.Equal("cat.png", file.FileName);
        Assert.Equal("image/png", file.ContentType);
    }
}