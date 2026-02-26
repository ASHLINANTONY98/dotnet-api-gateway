using System.Net;
using System.Text.Json;

namespace ESS.WebAPI.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        public readonly ILogger<GlobalExceptionMiddleware> _logger;

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
                        case (int)HttpStatusCode.InternalServerError:
                            await WriteErrorResponse(context, "Internal Server Error");
                            break;
                        case (int)HttpStatusCode.ServiceUnavailable: // 503
                            await WriteErrorResponse(context, "Service Unavailable"); 
                            break;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error forwarding request to Private API at {Path}", context.Request.Path);
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway; // 502
                if (!context.Response.HasStarted)
                {
                    await WriteErrorResponse(context, "Unauthorized");
                }
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "Timeout at {Path}", context.Request.Path);
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout; // 408
                if (!context.Response.HasStarted)
                {
                    await WriteErrorResponse(context, "Request timed out");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                if (!context.Response.HasStarted)
                {
                    await WriteErrorResponse(context, "Internal Server Error");
                }
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, string message)
        {
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = message,
                traceId = context.TraceIdentifier,
                path = context.Request.Path
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
