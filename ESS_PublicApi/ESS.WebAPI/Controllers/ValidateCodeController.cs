using ESS.Application.DTOs;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/validate")]
    //[AllowAnonymous]
    [Authorize(Roles = "VERIFICATION")]
    public class ValidateCodeController(HttpForwardingService forwarder, ILogger<ValidateCodeController> logger) : ControllerBase
    {
        private readonly HttpForwardingService _forwarder = forwarder;
        private readonly ILogger<ValidateCodeController> _logger = logger;

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
            if (response.Status == 400) 
            { 
                return BadRequest(new 
                { 
                    errors = response.Message, 
                    traceId = HttpContext.TraceIdentifier, 
                    path = HttpContext.Request.Path 
                }); 
            }

            return Ok(response); // Return the Private API response directly
        }
    }
}
