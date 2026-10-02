using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace ESS.Tests;

public class AuthenticationPipelineTests
    : IClassFixture<PrivateApiFactory>
{
    private readonly HttpClient _client;

    public AuthenticationPipelineTests(PrivateApiFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task ValidateCode_WithoutToken_Returns401()
    {
        // Arrange
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/ValidateCode/validate");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(
            System.Net.HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ValidateCode_WithWrongRole_Returns403()
    {
        // Arrange
        var token = CreateToken("Employee");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/ValidateCode/validate");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(
            System.Net.HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(PrivateApiFactory.TestJwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "TEST-VENDOR-001"),

            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: "ESS.IntegrationTests",
            audience: "ESS.IntegrationTestClients",
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-10),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    [Fact]
    public async Task ValidateCode_WithSupplierRole_ReturnsInvalidCodeResult()
    {
        var token = CreateToken("Supplier");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/ValidateCode/validate");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);

        request.Content = JsonContent.Create(new
        {
            EmpCode = 1001,
            AuthenticationCode = "123456"
        });

        using var response = await _client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "Invalid Authentication Code",
            responseBody);
    }

    [Fact]
    public async Task ValidateCode_WithValidSupplierToken_ReturnsSuccess()
    {
        var token = CreateToken("Supplier");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/ValidateCode/validate");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);

        request.Content = JsonContent.Create(new
        {
            EmpCode = 1001,
            AuthenticationCode = "654321"
        });

        using var response = await _client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "Authentication Code valid",
            responseBody);
    }

    [Fact]
    public async Task ValidateCode_WithExpiredSupplierToken_ReturnsExpired()
    {
        // Arrange
        var token = CreateToken("Supplier");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/ValidateCode/validate");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);

        request.Content = JsonContent.Create(new
        {
            EmpCode = 1001,
            AuthenticationCode = "111111"
        });

        // Act
        using var response = await _client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "Authentication Code expired",
            responseBody);
    }
}