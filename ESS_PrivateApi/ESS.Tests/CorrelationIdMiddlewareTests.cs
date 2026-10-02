using ESS.WebAPI.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ESS.Tests.Middleware
{
    public class CorrelationIdMiddlewareTests
    {
        private const string HeaderName = "X-Correlation-ID";

        [Fact]
        public async Task Invoke_ValidCorrelationId_PreservesId()
        {
            // Arrange
            var expectedId = Guid.NewGuid().ToString("D");
            var context = new DefaultHttpContext();

            context.Request.Headers[HeaderName] = expectedId;

            var middleware = new CorrelationIdMiddleware(
                _ => Task.CompletedTask);

            // Act
            await middleware.Invoke(context);

            // Assert
            Assert.Equal(
                expectedId,
                context.Request.Headers[HeaderName].ToString());

            Assert.Equal(
                expectedId,
                context.Response.Headers[HeaderName].ToString());
        }

        [Fact]
        public async Task Invoke_MissingCorrelationId_GeneratesNewId()
        {
            // Arrange
            var context = new DefaultHttpContext();

            var middleware = new CorrelationIdMiddleware(
                _ => Task.CompletedTask);

            // Act
            await middleware.Invoke(context);

            // Assert
            var requestId =
                context.Request.Headers[HeaderName].ToString();

            var responseId =
                context.Response.Headers[HeaderName].ToString();

            Assert.True(Guid.TryParseExact(requestId, "D", out _));
            Assert.Equal(requestId, responseId);
        }

        [Fact]
        public async Task Invoke_InvalidCorrelationId_GeneratesNewId()
        {
            // Arrange
            var context = new DefaultHttpContext();

            context.Request.Headers[HeaderName] = "invalid-id";

            var middleware = new CorrelationIdMiddleware(
                _ => Task.CompletedTask);

            // Act
            await middleware.Invoke(context);

            // Assert
            var requestId =
                context.Request.Headers[HeaderName].ToString();

            var responseId =
                context.Response.Headers[HeaderName].ToString();

            Assert.True(Guid.TryParseExact(requestId, "D", out _));
            Assert.Equal(requestId, responseId);
            Assert.NotEqual("invalid-id", requestId);
        }
    }
}