namespace RagBackend.Domain.Models
{
    /// <summary>
    /// Request model used when a user asks a single question
    /// that should be answered by the RAG pipeline.
    /// 
    /// This class represents the data sent from the frontend
    /// to the backend when asking a question.
    /// </summary>
    public sealed class AnswerRequest
    {
        /// <summary>
        /// The question the user wants the system to answer.
        /// 
        /// This value may be null or empty when validating input,
        /// so controllers should always check it before use.
        /// </summary>
        public string? Question { get; set; }
    }
}