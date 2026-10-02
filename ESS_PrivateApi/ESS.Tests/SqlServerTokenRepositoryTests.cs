using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using ESS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ESS.Tests;

public class SqlServerTokenRepositoryTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task FindAsync_ReturnsToken_WhenDetailsAreValid()
    {
        using var db = CreateContext();

        db.EssSoftTokens.Add(new EssSoftTokens
        {
            EmpCode = 1001,
            AuthenticationCode = "AUTH123",
            GeneratedOn = DateTime.UtcNow,
            Status = 1
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerTokenRepository(db);

        var result = await repository.FindAsync(1001, "AUTH123");

        Assert.NotNull(result);
        Assert.Equal(1001, result.EmpCode);
        Assert.Equal("AUTH123", result.AuthenticationCode);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenEmployeeCodeDoesNotMatch()
    {
        using var db = CreateContext();

        db.EssSoftTokens.Add(new EssSoftTokens
        {
            EmpCode = 1001,
            AuthenticationCode = "AUTH123",
            GeneratedOn = DateTime.UtcNow,
            Status = 1
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerTokenRepository(db);

        var result = await repository.FindAsync(9999, "AUTH123");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenAuthenticationCodeDoesNotMatch()
    {
        using var db = CreateContext();

        db.EssSoftTokens.Add(new EssSoftTokens
        {
            EmpCode = 1001,
            AuthenticationCode = "AUTH123",
            GeneratedOn = DateTime.UtcNow,
            Status = 1
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerTokenRepository(db);

        var result = await repository.FindAsync(1001, "WRONG");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenTokenIsInactive()
    {
        using var db = CreateContext();

        db.EssSoftTokens.Add(new EssSoftTokens
        {
            EmpCode = 1001,
            AuthenticationCode = "AUTH123",
            GeneratedOn = DateTime.UtcNow,
            Status = 0
        });

        await db.SaveChangesAsync();

        var repository = new SqlServerTokenRepository(db);

        var result = await repository.FindAsync(1001, "AUTH123");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenTokenDoesNotExist()
    {
        using var db = CreateContext();

        var repository = new SqlServerTokenRepository(db);

        var result = await repository.FindAsync(1001, "UNKNOWN");

        Assert.Null(result);
    }
}