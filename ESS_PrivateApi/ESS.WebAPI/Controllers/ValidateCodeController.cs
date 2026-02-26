using ESS.Application.DTOs;
using ESS.Application.UseCases.ESS_SOFT_TOKENS;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/ValidateCode")]
    public class ValidateCodeController : ControllerBase
    {
        private readonly ValidateCode _useCase;
        private readonly ILogger<ValidateCodeController> _logger;

        public ValidateCodeController(ValidateCode useCase, ILogger<ValidateCodeController> logger)
        {
            _useCase = useCase;
            _logger = logger;
        }
        [Authorize(Roles = "VERIFICATION")]
        [HttpPost("validate")]
        public async Task<ActionResult<ValidateCodeResponseDto>> Validate([FromBody] ValidateCodeRequestDto dto, CancellationToken ct)
        {
            try
            {
                var response = await _useCase.ExecuteAsync(dto, ct);
                _logger.LogInformation("Validation result for employee {EmpCode}: {Message}", dto.EmpCode, response.Message);
                return Ok(response);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation failed for employee {EmpCode}. Errors: {Errors}", dto.EmpCode, ex.Errors);
                return BadRequest(new { error = "Validation failed", details = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation for employee {EmpCode}", dto.EmpCode);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }
    }
}
