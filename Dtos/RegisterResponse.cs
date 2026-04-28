namespace Nutriflow.Dtos
{
    public class RegisterResponse
    {
        public string Message { get; set; } = string.Empty;
        public UserDto? User { get; set; }
    }
}