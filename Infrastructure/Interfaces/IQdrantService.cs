using System.Collections.Generic;
using System.Threading.Tasks;
using RagBackend.Infrastructure.Interfaces;

namespace RagBackend.Infrastructure.Interfaces
{
    public interface IQdrantService
    {
        Task EnsureCollectionAsync(int vectorSize);
        Task<List<string>> SearchAsync(float[] embedding, int limit);
        Task UpsertAsync(float[] embedding, string chunk);
    }
}