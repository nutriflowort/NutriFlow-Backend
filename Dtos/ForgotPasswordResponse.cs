namespace Nutriflow.Dtos
{
    public class ForgotPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // DEV: numeric code or token returned so frontend can display it in console for testing
        public string Code { get; set; } = string.Empty;
        public string ResetLink { get; set; } = string.Empty;
    }
}
