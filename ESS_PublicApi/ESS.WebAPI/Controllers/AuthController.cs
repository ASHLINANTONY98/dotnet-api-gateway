using ESS.Application.DTOs;
using ESS.Infrastructure.Services;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(HttpForwardingService forwarder, JwtService jwt, ILogger<AuthController> logger) : ControllerBase
    {
        private readonly HttpForwardingService _forwarder = forwarder;
        public readonly JwtService _jwt = jwt;
        private readonly ILogger<AuthController> _logger = logger;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiKeyLoginRequest request)
        {
            _logger.LogInformation("Login attempt at {Path} with TraceId {TraceId}",
                HttpContext.Request.Path, HttpContext.TraceIdentifier);

            var vendor = await _forwarder.PostAsync<VendorResponse>(
                "api/auth/login", request);

            if (vendor is null)
            {
                _logger.LogWarning("Login failed for TraceId {TraceId}", HttpContext.TraceIdentifier);
                return Unauthorized(new { message = "Invalid API key" });
            }

            //Generate JWT token for client
            var token = _jwt.GenerateToken(vendor.VendorId, vendor.VendorName, vendor.VendorRole);
            _logger.LogInformation("Login successful for TraceId {TraceId}, VendorId {VendorId}", HttpContext.TraceIdentifier, vendor.VendorId);
            return Ok(new { token });
        }

    }
}