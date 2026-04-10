namespace RagBackend.Domain.Models
{
    /// Standard error shape returned by the API.
    public class ApiError
    {
        public string Message { get; set; } = string.Empty;

        public string ErrorCode { get; set; } = "UNKNOWN_ERROR";
    }
}