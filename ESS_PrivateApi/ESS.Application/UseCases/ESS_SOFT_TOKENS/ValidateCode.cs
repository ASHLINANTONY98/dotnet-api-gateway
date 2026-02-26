using ESS.Application.DTOs;
using ESS.Application.Enum;
using ESS.Domain.Abstractions;
using FluentValidation;

namespace ESS.Application.UseCases.ESS_SOFT_TOKENS
{
    public sealed class ValidateCode
    {
        private readonly IValidateCodeRepository _repo;
        private readonly IValidator<ValidateCodeRequestDto> _validator;

        public ValidateCode(IValidateCodeRepository repo, IValidator<ValidateCodeRequestDto> validator)
        {
            _repo = repo;
            _validator = validator;
        }

        public async Task<ValidateCodeResponseDto> ExecuteAsync(ValidateCodeRequestDto dto, CancellationToken ct = default)
        {
            var result = _validator.Validate(dto);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
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
            var TimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var generatedUtc = TimeZoneInfo.ConvertTimeToUtc(token.GeneratedOn, TimeZone);
            if (generatedUtc < DateTime.UtcNow.AddMinutes(-10))
            {
                return new ValidateCodeResponseDto
                {
                    Status = ValidationStatus.Expired,
                    Message = "Authentication Code expired"
                };
            }

            return new ValidateCodeResponseDto
            {
                Status = ValidationStatus.Success,
                Message = "Authentication Code valid"
            };
        }
    }
}
