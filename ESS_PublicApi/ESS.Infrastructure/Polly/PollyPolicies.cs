using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net.Http;

namespace ESS.Infrastructure.Polly
{
    public static class PollyPolicies
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(3, retryAttempt =>
                {
                    Console.WriteLine($"RETRY #{retryAttempt} at {DateTime.Now}");
                    return TimeSpan.FromSeconds(2);
                });
        }

        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(20),
                    onBreak: (ex, time) =>
                    {
                        Console.WriteLine($"CIRCUIT OPEN for {time.TotalSeconds}s");
                    },
                    onReset: () =>
                    {
                        Console.WriteLine("CIRCUIT CLOSED");
                    }
                );
        }

        // NEW: Timeout policy
        public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 seconds
        }

    }
}