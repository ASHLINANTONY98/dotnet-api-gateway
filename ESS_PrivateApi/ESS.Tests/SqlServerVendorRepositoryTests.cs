using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using ESS.Infrastructure.Repositories;
using ESS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ESS.Tests;

public class SqlServerVendorRepositoryTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetByApiKeyAsync_ReturnsActiveVendor_WhenApiKeyIsValid()
    {
        using var db = CreateContext();

        db.Vendors.Add(new Vendor
        {
            VendorId = "V001",
            VendorName = "Test Vendor",
            ApiKey = TokenHasher.Hash("valid-api-key"),
            IsActive = 1,
            VendorRole = "Vendor"
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByApiKeyAsync("valid-api-key");

        Assert.NotNull(result);
        Assert.Equal("V001", result.VendorId);
    }

    [Fact]
    public async Task GetByApiKeyAsync_ReturnsNull_WhenApiKeyIsInvalid()
    {
        using var db = CreateContext();

        db.Vendors.Add(new Vendor
        {
            VendorId = "V001",
            VendorName = "Test Vendor",
            ApiKey = TokenHasher.Hash("valid-api-key"),
            IsActive = 1,
            VendorRole = "Vendor"
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByApiKeyAsync("wrong-api-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByApiKeyAsync_ReturnsNull_WhenVendorIsInactive()
    {
        using var db = CreateContext();

        db.Vendors.Add(new Vendor
        {
            VendorId = "V001",
            VendorName = "Inactive Vendor",
            ApiKey = TokenHasher.Hash("valid-api-key"),
            IsActive = 0,
            VendorRole = "Vendor"
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByApiKeyAsync("valid-api-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByVendorIdAsync_ReturnsActiveVendor_WhenVendorExists()
    {
        using var db = CreateContext();

        db.Vendors.Add(new Vendor
        {
            VendorId = "V001",
            VendorName = "Test Vendor",
            ApiKey = "hashed-key",
            IsActive = 1,
            VendorRole = "Vendor"
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByVendorIdAsync("V001");

        Assert.NotNull(result);
        Assert.Equal("Test Vendor", result.VendorName);
    }

    [Fact]
    public async Task GetByVendorIdAsync_ReturnsNull_WhenVendorDoesNotExist()
    {
        using var db = CreateContext();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByVendorIdAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByVendorIdAsync_ReturnsNull_WhenVendorIsInactive()
    {
        using var db = CreateContext();

        db.Vendors.Add(new Vendor
        {
            VendorId = "V001",
            VendorName = "Inactive Vendor",
            ApiKey = "hashed-key",
            IsActive = 0,
            VendorRole = "Vendor"
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerVendorRepository(db);

        var result = await repository.GetByVendorIdAsync("V001");

        Assert.Null(result);
    }
}