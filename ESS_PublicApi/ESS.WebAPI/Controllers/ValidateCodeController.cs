using ESS.Application.DTOs;
using ESS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/validate")]
    //[AllowAnonymous]
    
    public class ValidateCodeController(HttpForwardingService forwarder, ILogger<ValidateCodeController> logger) : ControllerBase
    {
        private readonly HttpForwardingService _forwarder = forwarder;
        private readonly ILogger<ValidateCodeController> _logger = logger;

        [Authorize(Roles = "Supplier")]
        [HttpPost]
        public async Task<IActionResult> Validate([FromBody] ValidateCodeRequestDto request)
        {
            _logger.LogInformation("Forwarding validation request for employee {EmpCode}", request.EmpCode);
            var response = await _forwarder.PostAsync<ValidateCodeResponseDto>(
                "api/ValidateCode/validate", request);

            if (response is null)
            {
                _logger.LogWarning("Validation failed for employee {EmpCode}", request.EmpCode);
                return BadRequest(new { error = "Validation failed" });
            }

            return Ok(response); // Return the Private API response directly
        }
    }
}
    