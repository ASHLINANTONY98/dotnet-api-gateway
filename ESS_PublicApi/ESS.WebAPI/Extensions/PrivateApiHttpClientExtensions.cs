using ESS.Infrastructure.Polly;
using ESS.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net.Http;

namespace ESS.WebAPI.Extensions;

public static class PrivateApiHttpClientExtensions
{
    public static IHttpClientBuilder AddPrivateApiHttpClient(
        this IServiceCollection services,
        Uri privateApiUri)
    {
        return services
            .AddHttpClient<HttpForwardingService>(client =>
            {
                client.BaseAddress = privateApiUri;
            })

            // Overall timeout
            .AddPolicyHandler(
                PollyPolicies.GetOverallTimeoutPolicy())

            // Circuit breaker
            .AddPolicyHandler((serviceProvider, request) =>
                PollyPolicies.GetCircuitBreakerPolicy(
                    serviceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(
                            "ESS.Infrastructure.Polly.CircuitBreaker")))

            // Retry GET requests only
            .AddPolicyHandler((serviceProvider, request) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    return PollyPolicies.GetRetryPolicy(
                        serviceProvider
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(
                                "ESS.Infrastructure.Polly.Retry"));
                }

                return Policy.NoOpAsync<HttpResponseMessage>();
            })

            // Per-attempt timeout
            .AddPolicyHandler(
                PollyPolicies.GetTimeoutPolicy());
    }
}