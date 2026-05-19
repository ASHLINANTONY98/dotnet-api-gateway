namespace ESS.Application.DTOs
{
    public class ErrorResponseDto
    {
        public string Message { get; set; }

        public ErrorResponseDto(string message)
        {
            Message = message;
        }
    }
}
