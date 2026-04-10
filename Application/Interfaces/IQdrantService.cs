using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagBackend.Application.Interfaces
{
    // Abstraction for vector storage and semantic search operations
    public interface IQdrantService
    {
        // Ensures the vector collection exists with the expected embedding size
        Task EnsureCollectionAsync(int vectorSize);

        // Searches for the most similar text chunks using a query embedding
        Task<List<string>> SearchAsync(float[] embedding, int limit);

        // Stores a text chunk and its embedding in the vector database
        Task UpsertAsync(float[] embedding, string chunk);
    }
}