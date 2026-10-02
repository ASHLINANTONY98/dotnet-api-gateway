using ESS.WebAPI.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace ESS.Tests;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenExceptionOccurs_Returns500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ =>
            throw new InvalidOperationException("Test exception");

        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.Equal(
            "application/json",
            context.Response.ContentType);
    }

    [Fact]
    public async Task Invoke_WhenExceptionOccurs_ReturnsErrorJsonWithTraceId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-123";

        RequestDelegate next = _ =>
            throw new Exception("Something went wrong");

        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        // Act
        await middleware.Invoke(context);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;

        // Assert
        Assert.Equal(
            "Internal Server Error",
            root.GetProperty("error").GetString());

        Assert.Equal(
            "test-trace-123",
            root.GetProperty("traceId").GetString());
    }
    [Fact]
    public async Task Invoke_WhenResponseHasStarted_RethrowsException()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var responseFeature =
            new TestResponseFeature
            {
                HasStarted = true
            };

        context.Features.Set<IHttpResponseFeature>(responseFeature);

        RequestDelegate next = _ =>
            throw new InvalidOperationException(
                "Exception after response started");

        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.Invoke(context));
    }

    private sealed class TestResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; }
            = new HeaderDictionary();

        public Stream Body { get; set; }
            = Stream.Null;

        public bool HasStarted { get; set; }

        public void OnStarting(
            Func<object, Task> callback,
            object state)
        {
        }

        public void OnCompleted(
            Func<object, Task> callback,
            object state)
        {
        }
    }
}