using ESS.Application.DTOs;
using ESS.Application.Enum;
using ESS.Application.UseCases.ESS_SOFT_TOKENS;
using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Security;
using ESS.Infrastructure.Services;
using ESS.WebAPI.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;
using Microsoft.AspNetCore.Http;
using ESS.Infrastructure.Persistence;
using ESS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ESS.Tests;

public class JwtServiceTests
{
    private const string SecretKey =
        "ThisIsATestSecretKeyThatIsLongEnough123!";

    private static IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["JwtSettings:Key"] = SecretKey,
            ["JwtSettings:Issuer"] = "ESS.Tests",
            ["JwtSettings:Audience"] = "ESS.TestClients",
            ["JwtSettings:ExpiresInMinutes"] = "10"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void GenerateToken_ShouldContainExpectedClaims()
    {
        // Arrange
        var service = new JwtService(CreateConfiguration());

        // Act
        var tokenString = service.GenerateToken(
            "vendor-123",
            "Test Vendor",
            "Admin");

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.Equal("ESS.Tests", token.Issuer);
        Assert.Contains("ESS.TestClients", token.Audiences);

        Assert.Equal(
            "vendor-123",
            token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);

        Assert.Equal(
            "Test Vendor",
            token.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);

        Assert.Equal(
            "Admin",
            token.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        Assert.False(string.IsNullOrWhiteSpace(
            token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
    }

    [Fact]
    public void GenerateToken_ShouldHaveValidSignatureAndExpiry()
    {
        // Arrange
        var service = new JwtService(CreateConfiguration());
        var handler = new JwtSecurityTokenHandler();

        // Act
        var tokenString = service.GenerateToken(
            "vendor-123",
            "Test Vendor",
            "Admin");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "ESS.Tests",

            ValidateAudience = true,
            ValidAudience = "ESS.TestClients",

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(SecretKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(
            tokenString,
            validationParameters,
            out var validatedToken);

        // Assert
        Assert.NotNull(principal);
        Assert.IsType<JwtSecurityToken>(validatedToken);

        var jwt = (JwtSecurityToken)validatedToken;

        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(11));
    }

    public class ValidateCodeTests
    {
        private readonly Mock<IValidateCodeRepository> _repoMock = new();
        private readonly Mock<IDistributedCache> _cacheMock = new();

        private readonly ValidateCode _sut;

        public ValidateCodeTests()
        {
            var validator = new ValidateCodeValidator();

            _sut = new ValidateCode(
                _repoMock.Object,
                validator,
                _cacheMock.Object);
        }

        private static ValidateCodeRequestDto CreateRequest(
            int empCode = 1001,
            string code = "123456")
        {
            return new ValidateCodeRequestDto
            {
                EmpCode = empCode,
                AuthenticationCode = code
            };
        }

        private static EssSoftTokens CreateToken(
            int empCode = 1001,
            string code = "123456",
            int minutesAgo = 1)
        {
            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var generatedUtc = DateTime.UtcNow.AddMinutes(-minutesAgo);

            var generatedIndiaTime =
                TimeZoneInfo.ConvertTimeFromUtc(
                    generatedUtc,
                    indiaTimeZone);

            return new EssSoftTokens
            {
                EmpCode = empCode,
                AuthenticationCode = code,
                GeneratedOn = generatedIndiaTime,
                Status = 1
            };
        }

        private void SetupCacheMiss()
        {
            _cacheMock
                .Setup(x => x.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRequestIsInvalid_ShouldThrowValidationException()
        {
            var request = CreateRequest(empCode: 0);

            await Assert.ThrowsAsync<ValidationException>(
                () => _sut.ExecuteAsync(request));

            _repoMock.Verify(
                x => x.FindAsync(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenDoesNotExist_ShouldReturnInvalid()
        {
            SetupCacheMiss();

            _repoMock
                .Setup(x => x.FindAsync(
                    1001,
                    "123456",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((EssSoftTokens?)null);

            var result = await _sut.ExecuteAsync(CreateRequest());

            Assert.Equal(ValidationStatus.Invalid, result.Status);
            Assert.Equal("Invalid Authentication Code", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenIsExpired_ShouldReturnExpired()
        {
            SetupCacheMiss();

            var token = CreateToken(minutesAgo: 11);

            _repoMock
                .Setup(x => x.FindAsync(
                    1001,
                    "123456",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var result = await _sut.ExecuteAsync(CreateRequest());

            Assert.Equal(ValidationStatus.Expired, result.Status);
            Assert.Equal("Authentication Code expired", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenWasGeneratedTooFarInFuture_ShouldReturnInvalid()
        {
            SetupCacheMiss();

            var token = CreateToken(minutesAgo: -4);

            _repoMock
                .Setup(x => x.FindAsync(
                    1001,
                    "123456",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var result = await _sut.ExecuteAsync(CreateRequest());

            Assert.Equal(ValidationStatus.Invalid, result.Status);
            Assert.Equal("Invalid Authentication Code", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenIsValid_ShouldReturnSuccessAndCacheResult()
        {
            SetupCacheMiss();

            var token = CreateToken(minutesAgo: 1);

            _repoMock
                .Setup(x => x.FindAsync(
                    1001,
                    "123456",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var result = await _sut.ExecuteAsync(CreateRequest());

            Assert.Equal(ValidationStatus.Success, result.Status);
            Assert.Equal("Authentication Code valid", result.Message);

            _cacheMock.Verify(
                x => x.SetAsync(
                    "validate:1001:123456",
                    It.Is<byte[]>(bytes =>
                        Encoding.UTF8.GetString(bytes) == "valid"),
                    It.Is<DistributedCacheEntryOptions>(options =>
                        options.AbsoluteExpirationRelativeToNow.HasValue &&
                        options.AbsoluteExpirationRelativeToNow.Value <=
                        TimeSpan.FromSeconds(30)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenValidResultIsCached_ShouldNotCallRepository()
        {
            _cacheMock
                .Setup(x => x.GetAsync(
                    "validate:1001:123456",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("valid"));

            var result = await _sut.ExecuteAsync(CreateRequest());

            Assert.Equal(ValidationStatus.Success, result.Status);
            Assert.Equal(
                "Authentication Code valid (cached)",
                result.Message);

            _repoMock.Verify(
                x => x.FindAsync(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void Validator_WhenRequestIsValid_ShouldPass()
        {
            // Arrange
            var validator = new ValidateCodeValidator();
            var request = CreateRequest();

            // Act
            var result = validator.Validate(request);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validator_WhenEmployeeCodeIsZero_ShouldFail()
        {
            var validator = new ValidateCodeValidator();
            var request = CreateRequest(empCode: 0);

            var result = validator.Validate(request);

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                error => error.PropertyName == nameof(request.EmpCode));
        }

        [Fact]
        public void Validator_WhenAuthenticationCodeIsEmpty_ShouldFail()
        {
            var validator = new ValidateCodeValidator();
            var request = CreateRequest(code: "");

            var result = validator.Validate(request);

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                error => error.PropertyName == nameof(request.AuthenticationCode));
        }

        [Fact]
        public void Validator_WhenAuthenticationCodeExceedsSixCharacters_ShouldFail()
        {
            var validator = new ValidateCodeValidator();
            var request = CreateRequest(code: "1234567");

            var result = validator.Validate(request);

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                error => error.PropertyName == nameof(request.AuthenticationCode));
        }

        [Fact]
        public void Validator_WhenAuthenticationCodeContainsLetters_ShouldFail()
        {
            var validator = new ValidateCodeValidator();
            var request = CreateRequest(code: "12AB56");

            var result = validator.Validate(request);

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                error => error.PropertyName == nameof(request.AuthenticationCode));
        }

        public class ValidateCodeControllerTests
        {
            [Fact]
            public async Task Validate_WhenCodeIsValid_ShouldReturnOkWithResponse()
            {
                // Arrange
                var repoMock = new Mock<IValidateCodeRepository>();
                var cacheMock = new Mock<IDistributedCache>();

                cacheMock
                    .Setup(x => x.GetAsync(
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((byte[]?)null);

                var indiaTimeZone =
                    TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                var generatedIndiaTime =
                    TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.UtcNow.AddMinutes(-1),
                        indiaTimeZone);

                repoMock
                    .Setup(x => x.FindAsync(
                        1001,
                        "123456",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new EssSoftTokens
                    {
                        EmpCode = 1001,
                        AuthenticationCode = "123456",
                        GeneratedOn = generatedIndiaTime,
                        Status = 1
                    });

                var useCase = new ValidateCode(
                    repoMock.Object,
                    new ValidateCodeValidator(),
                    cacheMock.Object);

                var logger =
                    NullLogger<ValidateCodeController>.Instance;

                var controller =
                    new ValidateCodeController(useCase, logger);

                var request = new ValidateCodeRequestDto
                {
                    EmpCode = 1001,
                    AuthenticationCode = "123456"
                };

                // Act
                var actionResult = await controller.Validate(
                    request,
                    CancellationToken.None);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(
                    actionResult.Result);

                var response =
                    Assert.IsType<ValidateCodeResponseDto>(
                        okResult.Value);

                Assert.Equal(ValidationStatus.Success, response.Status);
                Assert.Equal("Authentication Code valid", response.Message);

                repoMock.Verify(
                    x => x.FindAsync(
                        1001,
                        "123456",
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            public class AuthControllerTests
            {
                private const string SecretKey =
                    "ThisIsATestSecretKeyThatIsLongEnough123!";

                private static JwtService CreateJwtService()
                {
                    var settings = new Dictionary<string, string?>
                    {
                        ["JwtSettings:Key"] = SecretKey,
                        ["JwtSettings:Issuer"] = "ESS.Tests",
                        ["JwtSettings:Audience"] = "ESS.TestClients",
                        ["JwtSettings:ExpiresInMinutes"] = "10"
                    };

                    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(settings)
                        .Build();

                    return new JwtService(configuration);
                }
                private static AuthController CreateController(
                    Mock<IVendorRepository> vendorsMock,
                    Mock<IRefreshTokenRepository> refreshTokenMock)
                {
                    var controller = new AuthController(
                        vendorsMock.Object,
                        refreshTokenMock.Object,
                        NullLogger<AuthController>.Instance,
                        CreateJwtService());

                    controller.ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext()
                    };

                    return controller;
                }

                [Fact]
                public async Task Login_WhenApiKeyIsInvalid_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    vendorsMock
                        .Setup(x => x.GetByApiKeyAsync("invalid-key"))
                        .ReturnsAsync((Vendor?)null);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new ApiKeyLoginRequest("invalid-key");

                    // Act
                    var result = await controller.Login(request);

                    // Assert
                    var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

                    var response = Assert.IsType<ErrorResponseDto>(
                        unauthorized.Value);

                    Assert.Equal("Invalid API key", response.Message);

                    refreshTokenMock.Verify(
                        x => x.AddAsync(It.IsAny<RefreshToken>()),
                        Times.Never);
                }

                [Fact]
                public async Task Login_WhenApiKeyIsValid_ShouldReturnTokensAndStoreHashedRefreshToken()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    vendorsMock
                        .Setup(x => x.GetByApiKeyAsync("valid-api-key"))
                        .ReturnsAsync(new Vendor
                        {
                            VendorId = "vendor-123",
                            VendorName = "Test Vendor",
                            ApiKey = "valid-api-key",
                            VendorRole = "Supplier",
                            IsActive = 1
                        });

                    RefreshToken? savedToken = null;

                    refreshTokenMock
                        .Setup(x => x.AddAsync(It.IsAny<RefreshToken>()))
                        .Callback<RefreshToken>(token => savedToken = token)
                        .Returns(Task.CompletedTask);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new ApiKeyLoginRequest("valid-api-key");

                    // Act
                    var result = await controller.Login(request);

                    // Assert
                    var okResult = Assert.IsType<OkObjectResult>(result);

                    var response = Assert.IsType<AuthResponseDto>(
                        okResult.Value);

                    Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
                    Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));

                    Assert.NotNull(savedToken);
                    Assert.Equal("vendor-123", savedToken.VendorId);
                    Assert.False(savedToken.IsRevoked);
                    Assert.True(savedToken.ExpiryDate > DateTime.UtcNow);

                    // The raw token returned to the client must not be stored.
                    Assert.NotEqual(response.RefreshToken, savedToken.Token);

                    Assert.Equal(
                        TokenHasher.Hash(response.RefreshToken),
                        savedToken.Token);

                    refreshTokenMock.Verify(
                        x => x.AddAsync(It.IsAny<RefreshToken>()),
                        Times.Once);
                }

                private static RefreshToken CreateStoredToken(
                    string vendorId = "vendor-123",
                    bool isRevoked = false,
                    DateTime? expiryDate = null)
                {
                    return new RefreshToken
                    {
                        VendorId = vendorId,
                        Token = TokenHasher.Hash("old-refresh-token"),
                        ExpiryDate = expiryDate ?? DateTime.UtcNow.AddDays(7),
                        IsRevoked = isRevoked
                    };
                }

                [Fact]
                public async Task Refresh_WhenTokenDoesNotExist_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(
                            TokenHasher.Hash("invalid-token")))
                        .ReturnsAsync((RefreshToken?)null);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = "invalid-token"
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var unauthorized =
                        Assert.IsType<UnauthorizedObjectResult>(result);

                    var response =
                        Assert.IsType<ErrorResponseDto>(unauthorized.Value);

                    Assert.Equal("Invalid refresh token", response.Message);

                    vendorsMock.Verify(
                        x => x.GetByVendorIdAsync(It.IsAny<string>()),
                        Times.Never);
                }

                [Fact]
                public async Task Refresh_WhenTokenIsRevoked_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(
                            TokenHasher.Hash("old-refresh-token")))
                        .ReturnsAsync(CreateStoredToken(isRevoked: true));

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = "old-refresh-token"
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var unauthorized =
                        Assert.IsType<UnauthorizedObjectResult>(result);

                    var response =
                        Assert.IsType<ErrorResponseDto>(unauthorized.Value);

                    Assert.Equal("Token revoked", response.Message);
                }

                [Fact]
                public async Task Refresh_WhenTokenIsExpired_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(
                            TokenHasher.Hash("old-refresh-token")))
                        .ReturnsAsync(CreateStoredToken(
                            expiryDate: DateTime.UtcNow.AddMinutes(-1)));

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = "old-refresh-token"
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var unauthorized =
                        Assert.IsType<UnauthorizedObjectResult>(result);

                    var response =
                        Assert.IsType<ErrorResponseDto>(unauthorized.Value);

                    Assert.Equal("Token expired", response.Message);
                }

                [Fact]
                public async Task Refresh_WhenVendorDoesNotExist_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(
                            TokenHasher.Hash("old-refresh-token")))
                        .ReturnsAsync(CreateStoredToken());

                    vendorsMock
                        .Setup(x => x.GetByVendorIdAsync("vendor-123"))
                        .ReturnsAsync((Vendor?)null);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = "old-refresh-token"
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var unauthorized =
                        Assert.IsType<UnauthorizedObjectResult>(result);

                    var response =
                        Assert.IsType<ErrorResponseDto>(unauthorized.Value);

                    Assert.Equal("Vendor not found", response.Message);
                }

                [Fact]
                public async Task Refresh_WhenTokenIsValid_ShouldRotateTokenAndReturnNewTokens()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    const string oldRawToken = "old-refresh-token";
                    var oldTokenHash = TokenHasher.Hash(oldRawToken);

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(oldTokenHash))
                        .ReturnsAsync(CreateStoredToken());

                    vendorsMock
                        .Setup(x => x.GetByVendorIdAsync("vendor-123"))
                        .ReturnsAsync(new Vendor
                        {
                            VendorId = "vendor-123",
                            VendorName = "Test Vendor",
                            VendorRole = "Supplier",
                            ApiKey = "valid-api-key",
                            IsActive = 1
                        });

                    RefreshToken? rotatedToken = null;

                    refreshTokenMock
                        .Setup(x => x.RotateAsync(
                            oldTokenHash,
                            It.IsAny<RefreshToken>(),
                            It.IsAny<DateTime>()))
                        .Callback<string, RefreshToken, DateTime>(
                            (currentHash, newToken, nowUtc) =>
                                rotatedToken = newToken)
                        .ReturnsAsync(true);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = oldRawToken
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var okResult = Assert.IsType<OkObjectResult>(result);

                    var response =
                        Assert.IsType<AuthResponseDto>(okResult.Value);

                    Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
                    Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));

                    Assert.NotEqual(oldRawToken, response.RefreshToken);

                    Assert.NotNull(rotatedToken);
                    Assert.Equal("vendor-123", rotatedToken.VendorId);
                    Assert.False(rotatedToken.IsRevoked);
                    Assert.True(rotatedToken.ExpiryDate > DateTime.UtcNow);

                    // The database receives the hash, not the raw refresh token.
                    Assert.Equal(
                        TokenHasher.Hash(response.RefreshToken),
                        rotatedToken.Token);

                    Assert.NotEqual(
                        response.RefreshToken,
                        rotatedToken.Token);

                    refreshTokenMock.Verify(
                        x => x.RotateAsync(
                            oldTokenHash,
                            It.IsAny<RefreshToken>(),
                            It.IsAny<DateTime>()),
                        Times.Once);
                }

                [Fact]
                public async Task Refresh_WhenRotationFails_ShouldReturnUnauthorized()
                {
                    // Arrange
                    var vendorsMock = new Mock<IVendorRepository>();
                    var refreshTokenMock = new Mock<IRefreshTokenRepository>();

                    const string rawToken = "old-refresh-token";
                    var tokenHash = TokenHasher.Hash(rawToken);

                    refreshTokenMock
                        .Setup(x => x.GetByTokenAsync(tokenHash))
                        .ReturnsAsync(CreateStoredToken());

                    vendorsMock
                        .Setup(x => x.GetByVendorIdAsync("vendor-123"))
                        .ReturnsAsync(new Vendor
                        {
                            VendorId = "vendor-123",
                            VendorName = "Test Vendor",
                            VendorRole = "Supplier",
                            ApiKey = "valid-api-key",
                            IsActive = 1
                        });

                    refreshTokenMock
                        .Setup(x => x.RotateAsync(
                            tokenHash,
                            It.IsAny<RefreshToken>(),
                            It.IsAny<DateTime>()))
                        .ReturnsAsync(false);

                    var controller = CreateController(
                        vendorsMock,
                        refreshTokenMock);

                    var request = new RefreshTokenRequestDto
                    {
                        RefreshToken = rawToken
                    };

                    // Act
                    var result = await controller.Refresh(request);

                    // Assert
                    var unauthorized =
                        Assert.IsType<UnauthorizedObjectResult>(result);

                    var response =
                        Assert.IsType<ErrorResponseDto>(unauthorized.Value);

                    Assert.Equal(
                        "Invalid or expired refresh token",
                        response.Message);
                }

                public class RefreshTokenRepositoryTests
                {
                    private const string ConnectionString =
                        "Server=localhost;Database=EmployeeDB_Test;" +
                        "Trusted_Connection=True;TrustServerCertificate=True";

                    [Fact]
                    public async Task RotateAsync_WhenTokenIsValid_ShouldRevokeOldTokenAndInsertNewToken()
                    {
                        // Arrange
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldRawToken = Guid.NewGuid().ToString();
                        var newRawToken = Guid.NewGuid().ToString();

                        var oldTokenHash = TokenHasher.Hash(oldRawToken);
                        var newTokenHash = TokenHasher.Hash(newRawToken);

                        var oldToken = new RefreshToken
                        {
                            VendorId = "test-vendor",
                            Token = oldTokenHash,
                            ExpiryDate = DateTime.UtcNow.AddDays(7),
                            IsRevoked = false
                        };

                        var newToken = new RefreshToken
                        {
                            VendorId = "test-vendor",
                            Token = newTokenHash,
                            ExpiryDate = DateTime.UtcNow.AddDays(7),
                            IsRevoked = false
                        };

                        // Act
                        await using (var db = new ApplicationDbContext(options))
                        {
                            db.RefreshTokens.Add(oldToken);
                            await db.SaveChangesAsync();

                            var repository = new RefreshTokenRepository(db);

                            var result = await repository.RotateAsync(
                                oldTokenHash,
                                newToken,
                                DateTime.UtcNow);

                            // Assert
                            Assert.True(result);
                        }

                        // Verify persisted database state using a fresh DbContext.
                        await using (var verifyDb = new ApplicationDbContext(options))
                        {
                            var savedOldToken = await verifyDb.RefreshTokens
                                .SingleAsync(x => x.Token == oldTokenHash);

                            var savedNewToken = await verifyDb.RefreshTokens
                                .SingleAsync(x => x.Token == newTokenHash);

                            Assert.True(savedOldToken.IsRevoked);
                            Assert.False(savedNewToken.IsRevoked);
                            Assert.Equal("test-vendor", savedNewToken.VendorId);
                        }

                        // Clean up the test records.
                        await using (var cleanupDb = new ApplicationDbContext(options))
                        {
                            var testTokens = await cleanupDb.RefreshTokens
                                .Where(x =>
                                    x.Token == oldTokenHash ||
                                    x.Token == newTokenHash)
                                .ToListAsync();

                            cleanupDb.RefreshTokens.RemoveRange(testTokens);
                            await cleanupDb.SaveChangesAsync();
                        }
                    }
                    [Fact]
                    public async Task RotateAsync_WhenTokenIsExpired_ShouldReturnFalse()
                    {
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldHash = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var newHash = TokenHasher.Hash(Guid.NewGuid().ToString());

                        await using (var db = new ApplicationDbContext(options))
                        {
                            db.RefreshTokens.Add(new RefreshToken
                            {
                                VendorId = "test-vendor",
                                Token = oldHash,
                                ExpiryDate = DateTime.UtcNow.AddMinutes(-5),
                                IsRevoked = false
                            });

                            await db.SaveChangesAsync();

                            var repository = new RefreshTokenRepository(db);

                            var result = await repository.RotateAsync(
                                oldHash,
                                new RefreshToken
                                {
                                    VendorId = "test-vendor",
                                    Token = newHash,
                                    ExpiryDate = DateTime.UtcNow.AddDays(7),
                                    IsRevoked = false
                                },
                                DateTime.UtcNow);

                            Assert.False(result);
                        }

                        await using (var verifyDb = new ApplicationDbContext(options))
                        {
                            var oldToken = await verifyDb.RefreshTokens
                                .SingleAsync(x => x.Token == oldHash);

                            Assert.False(oldToken.IsRevoked);

                            Assert.False(await verifyDb.RefreshTokens
                                .AnyAsync(x => x.Token == newHash));
                        }

                        await CleanupTokens(options, oldHash, newHash);
                    }

                    [Fact]
                    public async Task RotateAsync_WhenTokenIsRevoked_ShouldReturnFalse()
                    {
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldHash = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var newHash = TokenHasher.Hash(Guid.NewGuid().ToString());

                        await using (var db = new ApplicationDbContext(options))
                        {
                            db.RefreshTokens.Add(new RefreshToken
                            {
                                VendorId = "test-vendor",
                                Token = oldHash,
                                ExpiryDate = DateTime.UtcNow.AddDays(7),
                                IsRevoked = true
                            });

                            await db.SaveChangesAsync();

                            var repository = new RefreshTokenRepository(db);

                            var result = await repository.RotateAsync(
                                oldHash,
                                new RefreshToken
                                {
                                    VendorId = "test-vendor",
                                    Token = newHash,
                                    ExpiryDate = DateTime.UtcNow.AddDays(7),
                                    IsRevoked = false
                                },
                                DateTime.UtcNow);

                            Assert.False(result);
                        }

                        await using (var verifyDb = new ApplicationDbContext(options))
                        {
                            var oldToken = await verifyDb.RefreshTokens
                                .SingleAsync(x => x.Token == oldHash);

                            Assert.True(oldToken.IsRevoked);

                            Assert.False(await verifyDb.RefreshTokens
                                .AnyAsync(x => x.Token == newHash));
                        }

                        await CleanupTokens(options, oldHash, newHash);
                    }

                    [Fact]
                    public async Task RotateAsync_WhenTokenDoesNotExist_ShouldReturnFalse()
                    {
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldHash = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var newHash = TokenHasher.Hash(Guid.NewGuid().ToString());

                        await using (var db = new ApplicationDbContext(options))
                        {
                            var repository = new RefreshTokenRepository(db);

                            var result = await repository.RotateAsync(
                                oldHash,
                                new RefreshToken
                                {
                                    VendorId = "test-vendor",
                                    Token = newHash,
                                    ExpiryDate = DateTime.UtcNow.AddDays(7),
                                    IsRevoked = false
                                },
                                DateTime.UtcNow);

                            Assert.False(result);
                        }

                        await using (var verifyDb = new ApplicationDbContext(options))
                        {
                            Assert.False(await verifyDb.RefreshTokens
                                .AnyAsync(x => x.Token == oldHash));

                            Assert.False(await verifyDb.RefreshTokens
                                .AnyAsync(x => x.Token == newHash));
                        }

                        await CleanupTokens(options, oldHash, newHash);
                    }

                    private static async Task CleanupTokens(
                        DbContextOptions<ApplicationDbContext> options,
                        params string[] hashes)
                    {
                        await using var db = new ApplicationDbContext(options);

                        var tokens = await db.RefreshTokens
                            .Where(x => hashes.Contains(x.Token))
                            .ToListAsync();

                        db.RefreshTokens.RemoveRange(tokens);
                        await db.SaveChangesAsync();
                    }

                    [Fact]
                    public async Task RotateAsync_WhenReplacementInsertFails_ShouldRollbackOldTokenRevocation()
                    {
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldHash = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var duplicateHash = TokenHasher.Hash(Guid.NewGuid().ToString());

                        try
                        {
                            // Arrange: insert an active old token and an existing token.
                            await using (var db = new ApplicationDbContext(options))
                            {
                                db.RefreshTokens.AddRange(
                                    new RefreshToken
                                    {
                                        VendorId = "test-vendor",
                                        Token = oldHash,
                                        ExpiryDate = DateTime.UtcNow.AddDays(7),
                                        IsRevoked = false
                                    },
                                    new RefreshToken
                                    {
                                        VendorId = "test-vendor",
                                        Token = duplicateHash,
                                        ExpiryDate = DateTime.UtcNow.AddDays(7),
                                        IsRevoked = false
                                    });

                                await db.SaveChangesAsync();
                            }

                            // Act: try to insert a replacement with a duplicate token hash.
                            await using (var db = new ApplicationDbContext(options))
                            {
                                var repository = new RefreshTokenRepository(db);

                                var replacement = new RefreshToken
                                {
                                    VendorId = "test-vendor",
                                    Token = duplicateHash, // Deliberate duplicate
                                    ExpiryDate = DateTime.UtcNow.AddDays(7),
                                    IsRevoked = false
                                };

                                await Assert.ThrowsAsync<DbUpdateException>(
                                    () => repository.RotateAsync(
                                        oldHash,
                                        replacement,
                                        DateTime.UtcNow));
                            }

                            // Assert: the transaction rolled back the old token's revocation.
                            await using (var verifyDb = new ApplicationDbContext(options))
                            {
                                var oldToken = await verifyDb.RefreshTokens
                                    .SingleAsync(x => x.Token == oldHash);

                                var existingToken = await verifyDb.RefreshTokens
                                    .SingleAsync(x => x.Token == duplicateHash);

                                Assert.False(oldToken.IsRevoked);
                                Assert.False(existingToken.IsRevoked);

                                // The duplicate token still exists only once.
                                var duplicateCount = await verifyDb.RefreshTokens
                                    .CountAsync(x => x.Token == duplicateHash);

                                Assert.Equal(1, duplicateCount);
                            }
                        }
                        finally
                        {
                            // Clean up even if an assertion fails.
                            await CleanupTokens(options, oldHash, duplicateHash);
                        }
                    }

                    [Fact]
                    public async Task RotateAsync_WhenTwoRequestsUseSameToken_OnlyOneShouldSucceed()
                    {
                        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                            .UseSqlServer(ConnectionString)
                            .Options;

                        var oldHash = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var newHash1 = TokenHasher.Hash(Guid.NewGuid().ToString());
                        var newHash2 = TokenHasher.Hash(Guid.NewGuid().ToString());

                        try
                        {
                            // Arrange: create one active refresh token.
                            await using (var db = new ApplicationDbContext(options))
                            {
                                db.RefreshTokens.Add(new RefreshToken
                                {
                                    VendorId = "test-vendor",
                                    Token = oldHash,
                                    ExpiryDate = DateTime.UtcNow.AddDays(7),
                                    IsRevoked = false
                                });

                                await db.SaveChangesAsync();
                            }

                            // Act: both requests attempt to consume the same token.
                            async Task<bool> RotateAsync(string newHash)
                            {
                                await using var db = new ApplicationDbContext(options);

                                var repository = new RefreshTokenRepository(db);

                                return await repository.RotateAsync(
                                    oldHash,
                                    new RefreshToken
                                    {
                                        VendorId = "test-vendor",
                                        Token = newHash,
                                        ExpiryDate = DateTime.UtcNow.AddDays(7),
                                        IsRevoked = false
                                    },
                                    DateTime.UtcNow);
                            }

                            var results = await Task.WhenAll(
                                RotateAsync(newHash1),
                                RotateAsync(newHash2));

                            // Assert: exactly one request succeeds.
                            Assert.Single(results.Where(result => result));

                            await using (var verifyDb = new ApplicationDbContext(options))
                            {
                                var oldToken = await verifyDb.RefreshTokens
                                    .SingleAsync(x => x.Token == oldHash);

                                Assert.True(oldToken.IsRevoked);

                                var replacementCount = await verifyDb.RefreshTokens
                                    .CountAsync(x =>
                                        x.Token == newHash1 ||
                                        x.Token == newHash2);

                                Assert.Equal(1, replacementCount);
                            }
                        }
                        finally
                        {
                            await CleanupTokens(
                                options,
                                oldHash,
                                newHash1,
                                newHash2);
                        }
                    }

                }
            }
        }

    }
}