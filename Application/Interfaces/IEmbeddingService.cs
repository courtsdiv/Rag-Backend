namespace RagBackend.Application.Interfaces
{
    // Converts text into an embedding vector for semantic searc
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}