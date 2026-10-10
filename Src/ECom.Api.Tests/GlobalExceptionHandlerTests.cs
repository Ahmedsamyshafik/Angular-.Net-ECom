using System.Text.Json;
using Ecom.Core.Exceptions;
using Ecom.Core.Pagination;
using ECom.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECom.Api.Tests;

public class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler handler, DefaultHttpContext context) CreateHandler(
        ILogger<GlobalExceptionHandler>? logger = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/product/GetById/1";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            logger ?? NullLogger<GlobalExceptionHandler>.Instance);

        return (handler, context);
    }

    private static async Task<string> ReadBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        return await reader.ReadToEndAsync();
    }

    private static ProblemDetails? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ProblemDetails>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    [Fact]
    public async Task NotFoundException_ShouldReturn404ProblemDetails()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context, new NotFoundException("Product not found."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("Product not found.", problem.Detail);
    }

    [Fact]
    public async Task BadRequestException_ShouldReturn400ProblemDetails()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context, new BadRequestException("Provide at least one product id."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("Provide at least one product id.", problem.Detail);
    }

    [Fact]
    public async Task ConflictException_ShouldReturn409ProblemDetails()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context, new ConflictException("Resource state conflict."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(409, problem!.Status);
        Assert.Equal("Conflict", problem.Title);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_ShouldReturn409WithSafeMessage()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new DbUpdateConcurrencyException("The database operation was expected to affect 1 row(s)..."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(409, problem!.Status);
        Assert.Equal("The record was modified by another user. Please reload and try again.", problem.Detail);
    }

    [Fact]
    public async Task BadHttpRequestException_ShouldReturn400WithSafeMessage()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context, new BadHttpRequestException("Failed to read request body including sensitive data."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.DoesNotContain("sensitive", problem!.Detail);
    }

    [Fact]
    public async Task ArgumentOutOfRangeException_ShouldReturn400WithItsMessage()
    {
        var (handler, context) = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new ArgumentOutOfRangeException(nameof(Pagination<object>.PageSize), "Page size must be greater than zero."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var problem = Deserialize(await ReadBodyAsync(context.Response));
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("Page size must be greater than zero. (Parameter 'PageSize')", problem.Detail);
    }

    [Fact]
    public async Task UnexpectedException_ShouldReturn500GenericProblemDetailsAndLogError()
    {
        var logger = new Mock<ILogger<GlobalExceptionHandler>>();
        var (handler, context) = CreateHandler(logger.Object);

        var exception = new InvalidOperationException("boom - conn-string=Server=secretDb; 0000:STACK");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var body = await ReadBodyAsync(context.Response);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("secretDb", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("STACK", body);

        var problem = Deserialize(body);
        Assert.NotNull(problem);
        Assert.Equal(500, problem!.Status);
        Assert.Equal("Internal Server Error", problem.Title);
        Assert.Equal("An unexpected error occurred. Please try again later.", problem.Detail);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }
}