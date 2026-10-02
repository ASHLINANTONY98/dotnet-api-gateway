using Serilog.Context;

namespace ESS.WebAPI.Middleware
{
    public class CorrelationIdMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;
        private const string HeaderName = "X-Correlation-ID";

        public async Task Invoke(HttpContext context)
        {
            var correlationId =
                context.Request.Headers[HeaderName].FirstOrDefault();

            // Accept only a standard GUID in D format.
            if (!Guid.TryParseExact(correlationId, "D", out var parsedId))
            {
                correlationId = Guid.NewGuid().ToString();
            }
            else
            {
                correlationId = parsedId.ToString("D");
            }

            // Use the validated ID for the request and response.
            context.Request.Headers[HeaderName] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                System.Diagnostics.Activity.Current?
                    .SetTag("correlation.id", correlationId);

                await _next(context);
            }
        }
    }
}