using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ESS.Tests;

public class HealthCheckTests
    : IClassFixture<PrivateApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(PrivateApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(content);

        Assert.Equal(
            "Healthy",
            document.RootElement
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public async Task Health_ReturnsSqlServerAndRedisChecks()
    {
        // Act
        var response = await _client.GetAsync("/health");

        var content = await response.Content.ReadAsStringAsync();

        // Assert
        using var document = JsonDocument.Parse(content);

        var checks = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, checks.Count);

        Assert.Contains(checks, check =>
            check.GetProperty("name").GetString() == "sqlserver");

        Assert.Contains(checks, check =>
            check.GetProperty("name").GetString() == "redis");

        Assert.All(checks, check =>
            Assert.Equal(
                "Healthy",
                check.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Health_WhenSqlServerIsUnhealthy_Returns503()
    {
        using var factory = new PrivateApiFactory
        {
            SqlServerResult = HealthCheckResult.Unhealthy(
                "SQL Server connection failed")
        };

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        using var document = JsonDocument.Parse(content);

        Assert.Equal(
            "Unhealthy",
            document.RootElement
                .GetProperty("status")
                .GetString());

        var sqlServerCheck = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "sqlserver");

        Assert.Equal(
            "Unhealthy",
            sqlServerCheck.GetProperty("status").GetString());

        Assert.Equal(
            "Health check failed",
            sqlServerCheck.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Health_WhenRedisIsUnhealthy_Returns503()
    {
        using var factory = new PrivateApiFactory
        {
            RedisResult = HealthCheckResult.Unhealthy(
                "Redis connection failed")
        };

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        using var document = JsonDocument.Parse(content);

        Assert.Equal(
            "Unhealthy",
            document.RootElement
                .GetProperty("status")
                .GetString());

        var redisCheck = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "redis");

        Assert.Equal(
            "Unhealthy",
            redisCheck.GetProperty("status").GetString());

        Assert.Equal(
            "Health check failed",
            redisCheck.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Health_WhenBothDependenciesAreUnhealthy_Returns503()
    {
        using var factory = new PrivateApiFactory
        {
            SqlServerResult = HealthCheckResult.Unhealthy(),
            RedisResult = HealthCheckResult.Unhealthy()
        };

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        using var document = JsonDocument.Parse(content);

        Assert.Equal(
            "Unhealthy",
            document.RootElement
                .GetProperty("status")
                .GetString());

        var checks = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, checks.Count);

        Assert.All(checks, check =>
            Assert.Equal(
                "Unhealthy",
                check.GetProperty("status").GetString()));
    }
}