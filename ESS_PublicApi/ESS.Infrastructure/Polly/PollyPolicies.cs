using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net.Http;

namespace ESS.Infrastructure.Polly
{
    public static class PollyPolicies
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
            ILogger logger,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            var delay = retryDelay ?? TimeSpan.FromSeconds(2);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount,
                    _ => delay,
                    (outcome, delay, retryAttempt, context) =>
                    {
                        logger.LogWarning(
                            "HTTP retry {RetryAttempt} scheduled after {DelaySeconds} seconds",
                            retryAttempt,
                            delay.TotalSeconds);
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage>
            GetCircuitBreakerPolicy(
                ILogger logger,
                int handledEventsAllowedBeforeBreaking = 2,
                TimeSpan? durationOfBreak = null)
        {
            var breakDuration =
                durationOfBreak ?? TimeSpan.FromSeconds(20);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking,
                    breakDuration,
                    onBreak: (outcome, time) =>
                    {
                        logger.LogWarning(
                            outcome.Exception,
                            "HTTP circuit opened for {DurationSeconds} seconds",
                            time.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        logger.LogInformation(
                            "HTTP circuit closed; requests may proceed");
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(
            TimeSpan? timeout = null)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout ?? TimeSpan.FromSeconds(10));
        }

        public static IAsyncPolicy<HttpResponseMessage>
            GetOverallTimeoutPolicy(
                TimeSpan? timeout = null)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout ?? TimeSpan.FromSeconds(30));
        }
    }
}