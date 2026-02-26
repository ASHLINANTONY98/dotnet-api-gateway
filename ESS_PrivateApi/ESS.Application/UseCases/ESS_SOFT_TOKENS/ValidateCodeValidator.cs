using ESS.Application.DTOs;
using FluentValidation;

namespace ESS.Application.UseCases.ESS_SOFT_TOKENS
{
    public class ValidateCodeValidator : AbstractValidator<ValidateCodeRequestDto>
    {
        public ValidateCodeValidator()
        {
            // EmpCode must not be empty and must contain digits only
            RuleFor(x => x.EmpCode)
                .NotEmpty().WithMessage("Employee code is required.")
                .GreaterThan(0).WithMessage("Employee code must be numeric and greater than 0.");

            // AuthenticationCode must not be empty and max 6 characters
            RuleFor(x => x.AuthenticationCode)
                .NotEmpty().WithMessage("Authentication code is required.")
                .MaximumLength(6).WithMessage("Authentication code must not exceed 6 characters.")
                .Matches(@"^\d+$").WithMessage("Authentication code must contain digits only.");
        }
    }
}
