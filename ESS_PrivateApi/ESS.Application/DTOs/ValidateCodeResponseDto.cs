using ESS.Application.Enum;

namespace ESS.Application.DTOs
{
    public class ValidateCodeResponseDto
    {
        public ValidationStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
