namespace Nutriflow.DTOs
{
    public class RegisterResponse
    {
        public string Message { get; set; } = string.Empty;
        public UserDto? User { get; set; }
    }
}