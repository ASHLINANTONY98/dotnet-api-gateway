using ESS.Application.DTOs;
using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using ESS.Infrastructure.Security;
using ESS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IVendorRepository vendors, IRefreshTokenRepository refreshTokenRepo,
        ILogger<AuthController> logger, JwtService jwt) : ControllerBase
    {
        private readonly IVendorRepository _vendors = vendors;
        private readonly ILogger<AuthController> _logger = logger;
        private readonly JwtService _jwt = jwt;
        private readonly IRefreshTokenRepository _refreshTokenRepo = refreshTokenRepo;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiKeyLoginRequest request)
        {
            _logger.LogInformation(
                "Login attempt | Path: {Path} | TraceId: {TraceId}",
                HttpContext.Request.Path,
                HttpContext.TraceIdentifier);
            //yoBrEI1RMoK6QQM6XVhuj9ZxSzxRr3tZbZnHwlBYEoM=
            var vendor = await _vendors.GetByApiKeyAsync(request.ApiKey);
            if (vendor is null)
            {
                _logger.LogWarning(
                    "Unauthorized login attempt | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);
                //return Unauthorized();
                return Unauthorized(new ErrorResponseDto("Invalid API key"));
            }

            // Generate Access Token
            var accessToken = _jwt.GenerateToken(
                vendor.VendorId,
                vendor.VendorName,
                vendor.VendorRole
            );

            // Generate RAW refresh token (this goes to client)
            var rawToken = Guid.NewGuid().ToString();

            // Create entity with HASHED token (this goes to DB)
            var refreshTokenEntity = new RefreshToken
            {
                VendorId = vendor.VendorId,
                Token = TokenHasher.Hash(rawToken), //store HASH only
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };

            // Save using repository
            await _refreshTokenRepo.AddAsync(refreshTokenEntity);

            _logger.LogInformation(
                "Login success | VendorId: {VendorId} | TraceId: {TraceId}",
                vendor.VendorId,
                HttpContext.TraceIdentifier);

            // Return BOTH tokens
            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = rawToken
            });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
        {
            _logger.LogInformation(
                "Refresh attempt | TraceId: {TraceId}",
                HttpContext.TraceIdentifier);

            var hashed = TokenHasher.Hash(request.RefreshToken);

            // Get from DB
            var storedToken = await _refreshTokenRepo.GetByTokenAsync(hashed);

            if (storedToken is null)
            {
                _logger.LogWarning(
                    "Invalid refresh token | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);
                //return Unauthorized(new { message = "Invalid refresh token" });
                return Unauthorized(new ErrorResponseDto("Invalid refresh token"));
            }

            // Check revoked
            if (storedToken.IsRevoked)
            {
                _logger.LogWarning(
                    "Revoked refresh token used | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);
                return Unauthorized(new ErrorResponseDto("Token revoked"));
            }

            // Check expiry
            if (storedToken.ExpiryDate <= DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "Expired refresh token | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);
                return Unauthorized(new ErrorResponseDto("Token expired"));
            }


            var vendor = await _vendors.GetByVendorIdAsync(storedToken.VendorId);
            
            if (vendor is null)
            {
                _logger.LogWarning(
                    "Vendor not found for refresh token | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);
                return Unauthorized(new ErrorResponseDto("Vendor not found"));
            }
            
            // Generate new access token
            var accessToken = _jwt.GenerateToken(
                vendor.VendorId,
                vendor.VendorName,
                vendor.VendorRole
            );

            // ROTATE REFRESH TOKEN

            var newRefreshToken = Guid.NewGuid().ToString();

            var newTokenEntity = new RefreshToken
            {
                VendorId = vendor.VendorId,
                Token = TokenHasher.Hash(newRefreshToken),
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };

            // Atomically consume the old token and save the new one.
            var rotated = await _refreshTokenRepo.RotateAsync(
                hashed,
                newTokenEntity,
                DateTime.UtcNow);

            if (!rotated)
            {
                _logger.LogWarning(
                    "Refresh token already consumed, revoked, or expired | TraceId: {TraceId}",
                    HttpContext.TraceIdentifier);

                return Unauthorized(
                    new ErrorResponseDto("Invalid or expired refresh token"));
            }
            _logger.LogInformation(
                "Refresh completed | VendorId: {VendorId} | TraceId: {TraceId}",
                vendor.VendorId,
                HttpContext.TraceIdentifier);

            // Return BOTH tokens
            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
            });
        }
    }
}
