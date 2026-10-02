using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace ESS.Tests;

public class PrivateApiFactory : WebApplicationFactory<Program>
{
    public HealthCheckResult SqlServerResult { get; set; }
        = HealthCheckResult.Healthy();

    public HealthCheckResult RedisResult { get; set; }
        = HealthCheckResult.Healthy();

    public const string TestIssuer = "ESS.IntegrationTests";
    public const string TestAudience = "ESS.IntegrationTestClients";

    public const string TestJwtKey =
        "Integration-Test-Only-Key-12345678901234567890";

    // Keep only one public constructor.
    public PrivateApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "JwtSettings__Key", TestJwtKey);

        Environment.SetEnvironmentVariable(
            "JwtSettings__Issuer", TestIssuer);

        Environment.SetEnvironmentVariable(
            "JwtSettings__Audience", TestAudience);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            var vendorRepository = new Mock<IVendorRepository>();

            vendorRepository
                .Setup(x => x.GetByVendorIdAsync(It.IsAny<string>()))
                .ReturnsAsync(new Vendor
                {
                    VendorId = "TEST-VENDOR-001",
                    VendorName = "Integration Test Vendor",
                    ApiKey = "test-api-key",
                    IsActive = 1,
                    VendorRole = "Supplier"
                });

            services.RemoveAll<IVendorRepository>();
            services.AddSingleton(vendorRepository.Object);

            // Replace the real validation repository with a mock.
            var validateCodeRepository =
                new Mock<IValidateCodeRepository>();

            validateCodeRepository
                .Setup(x => x.FindAsync(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    int empCode,
                    string authenticationCode,
                    CancellationToken _) =>
                {
                    // Invalid code
                    if (authenticationCode == "123456")
                    {
                        return Task.FromResult<EssSoftTokens?>(null);
                    }

                    // Only these two codes are recognized
                    if (authenticationCode != "654321" &&
                        authenticationCode != "111111")
                    {
                        return Task.FromResult<EssSoftTokens?>(null);
                    }

                    var indiaTimeZone =
                        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                    // Valid code: generated now
                    // Expired code: generated 15 minutes ago
                    var generatedUtc = authenticationCode == "111111"
                        ? DateTime.UtcNow.AddMinutes(-15)
                        : DateTime.UtcNow;

                    var generatedOn = DateTime.SpecifyKind(
                        TimeZoneInfo.ConvertTimeFromUtc(
                            generatedUtc,
                            indiaTimeZone),
                        DateTimeKind.Unspecified);

                    return Task.FromResult<EssSoftTokens?>(
                        new EssSoftTokens
                        {
                            EmpCode = empCode,
                            AuthenticationCode = authenticationCode,
                            GeneratedOn = generatedOn,
                            Status = 1
                        });
                });


            services.RemoveAll<IValidateCodeRepository>();
            services.AddSingleton(validateCodeRepository.Object);

            // Use in-memory cache instead of Redis.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            // Replace real SQL Server and Redis health checks
            // with controlled test health checks.
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();

                options.Registrations.Add(
                new HealthCheckRegistration(
                    "sqlserver",
                    _ => new TestHealthCheck(SqlServerResult),
                    null,
                    null));

                options.Registrations.Add(
                    new HealthCheckRegistration(
                        "redis",
                        _ => new TestHealthCheck(RedisResult),
                        null,
                        null));
            });
        });
    }
}