using ESS.Application.DTOs;
using ESS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;

namespace ESS.WebAPI.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController(HttpForwardingService forwarder, ILogger<AuthController> logger) : ControllerBase
    {
        private readonly HttpForwardingService _forwarder = forwarder;
        private readonly ILogger<AuthController> _logger = logger;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiKeyLoginRequest request)
        {
            _logger.LogInformation("Login attempt at {Path} with TraceId {TraceId}",
                    HttpContext.Request.Path, HttpContext.TraceIdentifier);

            var response = await _forwarder.PostAsync<AuthResponseDto>(
                "api/auth/login", request);
            if (response is null)
            {
                return StatusCode(500, "Unexpected null response");
            }
            _logger.LogInformation("Login successful | TraceId: {TraceId}",
                HttpContext.TraceIdentifier);
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
        {
            _logger.LogInformation(
                "Forwarding refresh request | TraceId: {TraceId}",
                HttpContext.TraceIdentifier);
            var response = await _forwarder.PostAsync<AuthResponseDto>(
                "api/auth/refresh", request);
            if (response is null)
            {
                return StatusCode(500, "Unexpected null response");
            }
            _logger.LogInformation(
                "Refresh successful | TraceId: {TraceId}",
                HttpContext.TraceIdentifier);

            return Ok(response);
        }

    }
}