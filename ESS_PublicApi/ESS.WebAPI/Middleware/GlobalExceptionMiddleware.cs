using System.Net;
using System.Text.Json;
using Polly.CircuitBreaker;

namespace ESS.WebAPI.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);

                // HANDLE STATUS CODES
                if (!context.Response.HasStarted)
                {
                    switch (context.Response.StatusCode)
                    {
                        case (int)HttpStatusCode.BadRequest:
                            await WriteErrorResponse(context, "Bad Request");
                            break;

                        case (int)HttpStatusCode.Unauthorized:
                            await WriteErrorResponse(context, "Unauthorized");
                            break;

                        case (int)HttpStatusCode.Forbidden:
                            await WriteErrorResponse(context, "Forbidden");
                            break;

                        case (int)HttpStatusCode.NotFound:
                            await WriteErrorResponse(context, "Not Found");
                            break;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized request");

                context.Response.StatusCode = 401;
                await WriteErrorResponse(context, ex.Message);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker open");

                context.Response.StatusCode = 503;
                await WriteErrorResponse(context, "Service temporarily unavailable (circuit open)");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout");

                context.Response.StatusCode = 408;
                await WriteErrorResponse(context, "Request timeout");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Private API unreachable");

                context.Response.StatusCode = 502;
                await WriteErrorResponse(context, "Bad gateway (private API down)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");

                context.Response.StatusCode = 500;
                await WriteErrorResponse(context, "Internal server error");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, string message)
        {
            context.Response.ContentType = "application/json";

            var payload = new
            {
                success = false,
                message,
                traceId = context.TraceIdentifier,
                path = context.Request.Path.ToString()
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}