using ESS.Application.DTOs;
using ESS.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IVendorRepository _vendors;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IVendorRepository vendors, ILogger<AuthController> logger)
        {
            _vendors = vendors;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiKeyLoginRequest request)
        {
            _logger.LogInformation("Login attempt at {Path} with TraceId {TraceId}",
                HttpContext.Request.Path, HttpContext.TraceIdentifier);
            var vendor = await _vendors.GetByApiKeyAsync(request.ApiKey);
            if (vendor is null)
            {
                _logger.LogWarning("Unauthorized login attempt. TraceId {TraceId}", HttpContext.TraceIdentifier);
                return Unauthorized();
            }

            _logger.LogInformation("Login successful for Vendor {VendorId} ({VendorName})", vendor.VendorId, vendor.VendorName);

            return Ok(new VendorResponse(vendor.VendorId, vendor.VendorName, vendor.VendorRole));
        }
    }

    public record ApiKeyLoginRequest(string ApiKey);
}
