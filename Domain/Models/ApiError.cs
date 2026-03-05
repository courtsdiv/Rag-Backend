namespace RagBackend.Domain.Models
{
    /// <summary>
    /// Represents a consistent error shape returned by the API.
    /// Every error contains a human-friendly message and a machine-readable code.
    /// </summary>
    public class ApiError
    {
        // Human-friendly message for the user.
        public string Message { get; set; } = string.Empty;

        // Short code useful for logging, debugging or the frontend.
        public string ErrorCode { get; set; } = "UNKNOWN_ERROR";
    }
}