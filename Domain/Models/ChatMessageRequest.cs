namespace RagBackend.Domain.Models
{
    /// <summary>
    /// Request model sent from the chat user interface to the backend.
    /// 
    /// This class represents a single chat message typed by the user.
    /// It is used by the chat endpoint to receive user input.
    /// </summary>
    public sealed class ChatMessageRequest
    {
        /// <summary>
        /// The message entered by the user in the chat input box.
        /// 
        /// This value may be null or empty, so the controller
        /// is responsible for validating it before use.
        /// </summary>
        public string? Message { get; set; }
    }
}