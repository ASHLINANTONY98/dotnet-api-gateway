using ESS.Infrastructure.Polly;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using System.Threading;

namespace ESS.Tests;

public class PollyPoliciesTests
{
    private static readonly HttpResponseMessage Success =
        new(HttpStatusCode.OK);

    [Fact]
    public async Task RetryPolicy_RetriesTransientFailure_ThenSucceeds()
    {
        var attempts = 0;

        var policy = PollyPolicies.GetRetryPolicy(
            NullLogger.Instance,
            retryCount: 2,
            retryDelay: TimeSpan.Zero);

        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;

            return Task.FromResult(
                attempts < 3
                    ? new HttpResponseMessage(
                        HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK));
        });

        Assert.Equal(3, attempts);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        result.Dispose();
    }

    [Fact]
    public async Task RetryPolicy_DoesNotRetry_UnauthorizedResponse()
    {
        var attempts = 0;

        var policy = PollyPolicies.GetRetryPolicy(
            NullLogger.Instance,
            retryCount: 3,
            retryDelay: TimeSpan.Zero);

        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });

        Assert.Equal(1, attempts);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);

        result.Dispose();
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterTwoFailures()
    {
        var policy = PollyPolicies.GetCircuitBreakerPolicy(
            NullLogger.Instance,
            handledEventsAllowedBeforeBreaking: 2,
            durationOfBreak: TimeSpan.FromSeconds(5));

        async Task<HttpResponseMessage> Fail()
        {
            return await policy.ExecuteAsync(() =>
                Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.ServiceUnavailable)));
        }

        using var first = await Fail();
        using var second = await Fail();

        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(
            () => policy.ExecuteAsync(() =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK))));
    }

    [Fact]
    public async Task CircuitBreaker_AllowsRequestAfterBreakDuration()
    {
        var policy = PollyPolicies.GetCircuitBreakerPolicy(
            NullLogger.Instance,
            handledEventsAllowedBeforeBreaking: 1,
            durationOfBreak: TimeSpan.FromMilliseconds(100));

        using var failure = await policy.ExecuteAsync(() =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(
            () => policy.ExecuteAsync(() =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK))));

        await Task.Delay(150);

        using var success = await policy.ExecuteAsync(() =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)));

        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }

    [Fact]
    public async Task CircuitBreaker_ReopensWhenHalfOpenRequestFails()
    {
        var policy = PollyPolicies.GetCircuitBreakerPolicy(
            NullLogger.Instance,
            handledEventsAllowedBeforeBreaking: 1,
            durationOfBreak: TimeSpan.FromMilliseconds(200));

        // First failure opens the circuit.
        using var firstFailure = await policy.ExecuteAsync(() =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)));

        // Wait until the circuit can allow a trial request.
        await Task.Delay(300);

        // The half-open trial fails.
        using var trialFailure = await policy.ExecuteAsync(() =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            trialFailure.StatusCode);

        // The failed trial reopens the circuit.
        await Assert.ThrowsAsync<
            BrokenCircuitException<HttpResponseMessage>>(
            () => policy.ExecuteAsync(() =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK))));
    }
    [Fact]
    public async Task TimeoutPolicy_WhenOperationExceedsTimeout_ThrowsTimeoutRejectedException()
    {
        // Arrange
        var policy = PollyPolicies.GetTimeoutPolicy(
            TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(
            () => policy.ExecuteAsync(
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return new HttpResponseMessage(
                        HttpStatusCode.OK);
                },
                CancellationToken.None));
    }
}