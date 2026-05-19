using ESS.Application.DTOs;
using ESS.Application.Enum;
using ESS.Domain.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;

namespace ESS.Application.UseCases.ESS_SOFT_TOKENS
{
    public sealed class ValidateCode
    {
        private readonly IValidateCodeRepository _repo;
        private readonly IValidator<ValidateCodeRequestDto> _validator;
        private readonly IDistributedCache _cache;

        public ValidateCode(IValidateCodeRepository repo, IValidator<ValidateCodeRequestDto> validator, IDistributedCache cache)
        {
            _repo = repo;
            _validator = validator;
            _cache = cache;
        }

        public async Task<ValidateCodeResponseDto> ExecuteAsync(ValidateCodeRequestDto dto, CancellationToken ct = default)
        {
            var result = _validator.Validate(dto);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
            //redis cache setting
            var cacheKey = $"validate:{dto.EmpCode}:{dto.AuthenticationCode}";

            var cached = await _cache.GetStringAsync(cacheKey, ct);

            if (cached != null)
            {
                return new ValidateCodeResponseDto
                {
                    Status = cached == "valid"
                        ? ValidationStatus.Success
                        : ValidationStatus.Invalid,
                    Message = cached == "valid"
                        ? "Authentication Code valid (cached)"
                        : "Invalid Authentication Code (cached)"
                };
            }

            var token = await _repo.FindAsync(dto.EmpCode, dto.AuthenticationCode, ct);
            if (token is null)
            {
                await _cache.SetStringAsync(
                    cacheKey,
                    "invalid",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                    },
                ct);
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Invalid,
                    Message = "Invalid Authentication Code"
                };
            }
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var generatedUtc = TimeZoneInfo.ConvertTimeToUtc(token.GeneratedOn, timeZone);
            if (generatedUtc < DateTime.UtcNow.AddMinutes(-10))
            {
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Expired,
                    Message = "Authentication Code expired"
                };
            }
            await _cache.SetStringAsync(
                cacheKey,
                "valid",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                },
            ct);    

            return new ValidateCodeResponseDto
            {
                Status = ValidationStatus.Success,
                Message = "Authentication Code valid"
            };
        }
    }
}
