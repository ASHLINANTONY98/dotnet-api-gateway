using System.Net;
using System.Net.Http.Json;
using ESS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ESS.WebAPI.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace ESS.Tests;

public class HttpForwardingServiceTests
{
    [Fact]
    public async Task GetAsync_WhenApiReturnsSuccess_ReturnsDeserializedResponse()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContextAccessor = new HttpContextAccessor();

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var result = await service.GetAsync<TestResponse>(
            "api/test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Message);
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal(
            "https://localhost/api/test",
            handler.RequestUri);
    }
    [Fact]
    public async Task GetAsync_WhenPrivateApiReturnsUnauthorized_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContextAccessor = new HttpContextAccessor();

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetAsync<TestResponse>("api/test"));

        // Assert
        Assert.Equal(
            "Private API returned Unauthorized.",
            exception.Message);
    }

    private class TestResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpMethod? RequestMethod { get; private set; }
        public string? RequestUri { get; private set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue?AuthorizationHeader { get; private set; }
        public string? CorrelationIdHeader { get; private set; }
        public string? RequestBody { get; private set; }

        public FakeHttpMessageHandler(
            HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri?.ToString();
            AuthorizationHeader = request.Headers.Authorization;

            request.Headers.TryGetValues(
                "X-Correlation-ID",
                out var correlationIds);

            CorrelationIdHeader = correlationIds?.FirstOrDefault();

            RequestBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            return _response;
        }
    }

    private class CancellationTestHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.Infinite,
                cancellationToken);

            throw new InvalidOperationException(
                "This line should never be reached.");
        }
    }

    [Fact]
    public async Task GetAsync_WhenBearerTokenExists_ForwardsAuthorizationHeader()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer test-token";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        await service.GetAsync<TestResponse>("api/test");

        // Assert
        Assert.NotNull(handler.AuthorizationHeader);
        Assert.Equal("Bearer", handler.AuthorizationHeader!.Scheme);
        Assert.Equal("test-token", handler.AuthorizationHeader.Parameter);
    }

    [Fact]
    public async Task GetAsync_WhenPrivateApiReturnsInternalServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContextAccessor = new HttpContextAccessor();

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetAsync<TestResponse>("api/test"));

        // Assert
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);

        Assert.Equal(
            "Private API error: InternalServerError",
            exception.Message);
    }

    [Fact]
    public async Task GetAsync_WhenCorrelationIdExists_ForwardsCorrelationIdHeader()
    {
            // Arrange
            const string correlationId =
                "550e8400-e29b-41d4-a716-446655440000";

            var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = correlationId;

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        await service.GetAsync<TestResponse>("api/test");

        // Assert
        Assert.Equal(
            correlationId,
            handler.CorrelationIdHeader);
    }

    [Fact]
    public async Task PostAsync_WhenApiReturnsSuccess_ReturnsDeserializedResponse()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Created" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContextAccessor = new HttpContextAccessor();

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        var payload = new
        {
            Name = "Ashlin",
            Department = "IT"
        };

        // Act
        var result = await service.PostAsync<TestResponse>(
            "api/test",
            payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Created", result.Message);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);

        Assert.Equal(
            "https://localhost/api/test",
            handler.RequestUri);
    }

    [Fact]
    public async Task PostAsync_WhenPayloadIsProvided_SendsJsonBody()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        var payload = new
        {
            Name = "Ashlin",
            Department = "IT"
        };

        // Act
        await service.PostAsync<TestResponse>(
            "api/test",
            payload);

        // Assert
        Assert.NotNull(handler.RequestBody);

        var sentPayload =
            System.Text.Json.JsonDocument.Parse(handler.RequestBody);

        Assert.Equal(
            "Ashlin",
            sentPayload.RootElement.GetProperty("name").GetString());

        Assert.Equal(
            "IT",
            sentPayload.RootElement.GetProperty("department").GetString());
    }

    [Fact]
    public async Task PostAsync_WhenBearerTokenExists_ForwardsAuthorizationHeader()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer test-token";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        var payload = new
        {
            Name = "Ashlin"
        };

        // Act
        await service.PostAsync<TestResponse>(
            "api/test",
            payload);

        // Assert
        Assert.NotNull(handler.AuthorizationHeader);

        Assert.Equal(
            "Bearer",
            handler.AuthorizationHeader!.Scheme);

        Assert.Equal(
            "test-token",
            handler.AuthorizationHeader.Parameter);
    }

    [Fact]
    public async Task PostAsync_WhenCorrelationIdExists_ForwardsCorrelationIdHeader()
    {
        // Arrange
        const string correlationId =
            "550e8400-e29b-41d4-a716-446655440000";

        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] =
            correlationId;

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        await service.PostAsync<TestResponse>(
            "api/test",
            new { Name = "Ashlin" });

        // Assert
        Assert.Equal(
            correlationId,
            handler.CorrelationIdHeader);
    }

    [Fact]
    public async Task PostAsync_WhenPrivateApiReturnsUnauthorized_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.PostAsync<TestResponse>(
                    "api/test",
                    new { Name = "Ashlin" }));

        // Assert
        Assert.Equal(
            "Private API returned Unauthorized.",
            exception.Message);
    }

    [Fact]
    public async Task PostAsync_WhenPrivateApiReturnsInternalServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.PostAsync<TestResponse>(
                "api/test",
                new { Name = "Ashlin" }));

        // Assert
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);

        Assert.Equal(
            "Private API error: InternalServerError",
            exception.Message);
    }

    [Fact]
    public async Task GetAsync_WhenBearerTokenDoesNotExist_DoesNotForwardAuthorizationHeader()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        await service.GetAsync<TestResponse>("api/test");

        // Assert
        Assert.Null(handler.AuthorizationHeader);
    }

    [Fact]
    public async Task PostAsync_WhenBearerTokenDoesNotExist_DoesNotForwardAuthorizationHeader()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        await service.PostAsync<TestResponse>(
            "api/test",
            new { Name = "Ashlin" });

        // Assert
        Assert.Null(handler.AuthorizationHeader);
    }

    [Fact]
    public async Task GetAsync_WhenAuthorizationSchemeIsNotBearer_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();

        // A valid Authorization header, but not a Bearer token.
        httpContext.Request.Headers.Authorization = "Basic test-token";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.GetAsync<TestResponse>("api/test"));

        // Assert
        Assert.Equal(
            "Invalid Authorization header.",
            exception.Message);

        // The request must not reach the Private API.
        Assert.Null(handler.RequestMethod);
    }

    [Fact]
    public async Task GetAsync_WhenBearerTokenContainsSpaces_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization =
            "Bearer token with spaces";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.GetAsync<TestResponse>("api/test"));

        // Assert
        Assert.Equal(
            "Invalid Authorization header.",
            exception.Message);

        Assert.Null(handler.RequestMethod);
    }

    [Fact]
    public async Task GetAsync_WhenBearerTokenIsEmpty_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer ";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.GetAsync<TestResponse>("api/test"));

        // Assert
        Assert.Equal(
            "Invalid Authorization header.",
            exception.Message);

        Assert.Null(handler.RequestMethod);
    }

    [Fact]
    public async Task PostAsync_WhenAuthorizationSchemeIsNotBearer_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Basic test-token";

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpForwardingService(
            httpClient,
            httpContextAccessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.PostAsync<TestResponse>(
                    "api/test",
                    new { Name = "Ashlin" }));

        // Assert
        Assert.Equal(
            "Invalid Authorization header.",
            exception.Message);

        Assert.Null(handler.RequestMethod);
    }

    [Fact]
    public async Task PostAsync_WhenBearerTokenContainsSpaces_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer invalid token";

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            client,
            accessor,
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.PostAsync<TestResponse>(
                "api/test",
                new { Name = "Ashlin" }));

        // Assert
        Assert.Equal("Invalid Authorization header.", exception.Message);
        Assert.Null(handler.RequestMethod);
    }

    [Fact]
    public async Task GetAsync_WhenCallerCancelsRequest_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        var handler = new CancellationTestHandler();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var requestTask = service.GetAsync<TestResponse>(
            "api/test",
            cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => requestTask);
    }

    [Fact]
    public async Task GetAsync_WhenHttpClientTimesOut_ThrowsOperationCanceledException()
    {
        // Arrange
        var handler = new CancellationTestHandler();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/"),
            Timeout = TimeSpan.FromMilliseconds(200)
        };

        var service = new HttpForwardingService(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<HttpForwardingService>.Instance);

        // Act
        var requestTask = service.GetAsync<TestResponse>("api/test");

        // Assert
        var exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => requestTask);

        Assert.IsType<TimeoutException>(
            exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_WithRegisteredPipeline_RetriesAndSucceeds()
    {
        // Arrange
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TestResponse { Message = "Success" })
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddPrivateApiHttpClient(
                new Uri("https://private.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();

        var service =
            provider.GetRequiredService<HttpForwardingService>();

        // Act
        var result = await service.GetAsync<TestResponse>("api/test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Message);

        // First request: 500
        // Retry: 200
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task PostAsync_WithRegisteredPipeline_DoesNotRetry()
    {
        // Arrange
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddPrivateApiHttpClient(
                new Uri("https://private.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();

        var service =
            provider.GetRequiredService<HttpForwardingService>();

        // Act
        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.PostAsync<TestResponse>(
                    "api/test",
                    new { Name = "Ashlin" }));

        // Assert
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);

        // POST must not retry.
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int RequestCount { get; private set; }

        public SequenceHttpMessageHandler(
            params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No more fake responses are available.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                while (_responses.Count > 0)
                {
                    _responses.Dequeue().Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}