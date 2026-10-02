using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ESS.Tests;

public class TestHealthCheck : IHealthCheck
{
    private readonly HealthCheckResult _result;

    public TestHealthCheck(HealthCheckResult result)
    {
        _result = result;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_result);
    }
}