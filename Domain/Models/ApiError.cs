namespace RagBackend.Domain.Models
{
    /// <summary>
    /// Represents the standard error format returned by the API.
    /// 
    /// Whenever the backend needs to return an error to the frontend,
    /// it uses this structure so errors are predictable and easy to handle.
    /// </summary>
    public class ApiError
    {
        /// <summary>
        /// A human‑readable description of what went wrong.
        /// 
        /// This message is intended to be shown to the user
        /// or used for debugging in the UI.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// A machine‑readable error code.
        /// 
        /// This allows the frontend to react to specific errors
        /// without relying on the text message.
        /// </summary>
        public string ErrorCode { get; set; } = "UNKNOWN_ERROR";
    }
}
