namespace Nutriflow.Dtos
{
    public class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}