namespace RagBackend.Domain.Models
{
    /// Request model for asking a question in the RAG pipeline.
    public sealed class AnswerRequest
    {
        public string? Question { get; set; }
    }
}