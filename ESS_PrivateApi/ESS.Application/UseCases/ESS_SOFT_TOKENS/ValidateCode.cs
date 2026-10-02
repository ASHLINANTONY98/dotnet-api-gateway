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
                
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Invalid,
                    Message = "Invalid Authentication Code"
                };
            }
            // GeneratedOn is stored as India-local time.
            // SQL Server datetime2 does not preserve DateTime.Kind.
            var timeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var generatedLocal = DateTime.SpecifyKind(
                token.GeneratedOn,
                DateTimeKind.Unspecified);

            var generatedUtc = TimeZoneInfo.ConvertTimeToUtc(
                generatedLocal,
                timeZone);

            var nowUtc = DateTime.UtcNow;

            // Reject timestamps more than 2 minutes in the future.
            var clockSkewTolerance = TimeSpan.FromMinutes(2);

            if (generatedUtc > nowUtc.Add(clockSkewTolerance))
            {
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Invalid,
                    Message = "Invalid Authentication Code"
                };
            }

            // Authentication code expires after 10 minutes.
            var expiresAtUtc = generatedUtc.AddMinutes(10);
            var remainingValidity = expiresAtUtc - nowUtc;

            if (remainingValidity <= TimeSpan.Zero)
            {
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Expired,
                    Message = "Authentication Code expired"
                };
            }
            var cacheDuration = TimeSpan.FromSeconds(30);

            var cacheExpiry = remainingValidity < cacheDuration
                ? remainingValidity
                : cacheDuration;

            await _cache.SetStringAsync(
                cacheKey,
                "valid",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheExpiry
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
