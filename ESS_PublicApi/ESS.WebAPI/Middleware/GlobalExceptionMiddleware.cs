
using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ESS.WebAPI.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // A client disconnect is cancellation, not a server timeout.
                if (context.RequestAborted.IsCancellationRequested &&
                    ex is OperationCanceledException)
                {
                    _logger.LogInformation(
                        "Request cancelled by client | TraceId: {TraceId}",
                        context.TraceIdentifier);

                    return;
                }

                // The response cannot safely be replaced once started.
                if (context.Response.HasStarted)
                {
                    _logger.LogError(
                        ex,
                        "Exception after response started | TraceId: {TraceId}",
                        context.TraceIdentifier);

                    throw;
                }

                var (statusCode, message) = ex switch
                {
                    UnauthorizedAccessException =>
                        (StatusCodes.Status401Unauthorized,
                         "Unauthorized"),

                    BrokenCircuitException =>
                        (StatusCodes.Status503ServiceUnavailable,
                         "Service temporarily unavailable"),

                    TimeoutRejectedException =>
                        (StatusCodes.Status504GatewayTimeout,
                         "Upstream service timed out"),

                    // A TaskCanceledException not caused by a client
                    // disconnect can indicate an upstream HTTP timeout.
                    TaskCanceledException =>
                        (StatusCodes.Status504GatewayTimeout,
                         "Upstream service timed out"),

                    HttpRequestException =>
                        (StatusCodes.Status502BadGateway,
                         "Bad gateway"),

                    _ =>
                        (StatusCodes.Status500InternalServerError,
                         "Internal server error")
                };

                _logger.LogError(
                    ex,
                    "Request failed | StatusCode: {StatusCode} | TraceId: {TraceId}",
                    statusCode,
                    context.TraceIdentifier);

                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    success = false,
                    message,
                    traceId = context.TraceIdentifier,
                    path = context.Request.Path.ToString()
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(payload));
            }
        }
    }
}