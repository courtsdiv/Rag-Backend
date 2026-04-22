namespace RagBackend.Application.Interfaces
{
    /// <summary>
    /// Interface for a service that converts text into an embedding vector.
    /// 
    /// An embedding is a numeric representation of text that captures
    /// its meaning. These vectors are used for semantic search in the
    /// vector store.
    /// 
    /// Using an interface allows the application to stay independent
    /// of the specific embedding provider being used.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Converts a piece of text into an embedding vector.
        /// 
        /// This method is used when:
        /// - indexing documents into the vector store
        /// - converting user questions for semantic search
        /// </summary>
        /// <param name="text">
        /// The text to convert into an embedding vector.
        /// </param>
        /// <returns>
        /// A numeric vector that represents the meaning of the text.
        /// </returns>
        Task<float[]> GetEmbeddingAsync(string text);
    }
}
