using System.Text.Json;

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
                // Handle exceptions only.
                // Do not modify normal error responses.
                await _next(context);
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogError(
                        ex,
                        "Exception after response started | TraceId: {TraceId}",
                        context.TraceIdentifier);

                    throw;
                }

                _logger.LogError(
                    ex,
                    "Unhandled exception | TraceId: {TraceId}",
                    context.TraceIdentifier);

                context.Response.Clear();
                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    error = "Internal Server Error",
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(payload));
            }
        }
    }
}