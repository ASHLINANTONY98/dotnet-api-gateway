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
        //[Authorize(Roles = "Supplier")]
        //[Authorize]
        [HttpPost("validate")]
        public async Task<ActionResult<ValidateCodeResponseDto>> Validate([FromBody] ValidateCodeRequestDto dto, CancellationToken ct)
        {
            var response = await _useCase.ExecuteAsync(dto, ct);
            _logger.LogInformation("Validation result for employee {EmpCode}: {Message}", dto.EmpCode, response.Message);
            return Ok(response);
        }
    }
}
