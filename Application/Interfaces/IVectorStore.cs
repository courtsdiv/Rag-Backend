namespace RagBackend.Application.Interfaces
{
    /// <summary>
    /// Interface that represents a vector storage system.
    /// 
    /// A vector store is used to:
    /// - store numeric representations (embeddings) of text
    /// - search those embeddings to find relevant document content
    /// 
    /// This interface allows the application to remain independent
    /// of any specific vector database implementation.
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Ensures that the vector collection exists in the vector store.
        /// 
        /// This method is typically called before inserting or searching data.
        /// The vector size must match the size of the embeddings being stored.
        /// </summary>
        /// <param name="vectorSize">
        /// The expected size of each embedding vector.
        /// </param>
        Task EnsureCollectionAsync(int vectorSize);

        /// <summary>
        /// Searches the vector store for text chunks that are most similar
        /// to the provided embedding vector.
        /// </summary>
        /// <param name="embedding">
        /// The embedding vector created from a user query.
        /// </param>
        /// <param name="limit">
        /// The maximum number of similar results to return.
        /// </param>
        /// <returns>
        /// A list of text chunks that best match the query.
        /// </returns>
        Task<List<string>> SearchAsync(float[] embedding, int limit);

        /// <summary>
        /// Stores a text chunk and its corresponding embedding
        /// in the vector store.
        /// </summary>
        /// <param name="embedding">
        /// The numeric embedding that represents the meaning of the text.
        /// </param>
        /// <param name="chunk">
        /// The original text content associated with the embedding.
        /// </param>
        Task UpsertAsync(float[] embedding, string chunk);
    }
}
