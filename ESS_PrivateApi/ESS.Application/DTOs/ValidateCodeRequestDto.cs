namespace ESS.Application.DTOs
{
    public class ValidateCodeRequestDto
    {
        public int EmpCode { get; set; }
        public string AuthenticationCode { get; set; } = default!;
    }
}
